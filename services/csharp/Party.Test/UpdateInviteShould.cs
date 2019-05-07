using System.Collections.Generic;
using Grpc.Core;
using Improbable.OnlineServices.DataModel;
using Improbable.OnlineServices.Proto.Invite;
using MemoryStore;
using Moq;
using NUnit.Framework;
using InviteDataModel = Improbable.OnlineServices.DataModel.Party.Invite;
using InviteStatusDataModel = Improbable.OnlineServices.DataModel.Party.Invite.Status;
using InviteProto = Improbable.OnlineServices.Proto.Invite.Invite;
using InviteStatusProto = Improbable.OnlineServices.Proto.Invite.Invite.Types.Status;

namespace Party.Test
{
    [TestFixture]
    public class UpdateInviteShould
    {
        private const string SenderPlayerId = "Sender";
        private const string ReceiverPlayerId = "Receiver";
        private const string PartyId = "Party";

        private static readonly IDictionary<string, string> _metadata = new Dictionary<string, string>
        {
            {"name", "satellites"},
            {"timestamp", "07032019"}
        };

        private InviteDataModel _storedInvite;
        private InviteProto _updatedInvite;
        private Mock<ITransaction> _mockTransaction;
        private Mock<IMemoryStoreClient> _mockMemoryStoreClient;
        private InviteServiceImpl _inviteService;

        [SetUp]
        public void SetUp()
        {
            _storedInvite = new InviteDataModel(SenderPlayerId, ReceiverPlayerId, PartyId, _metadata);
            _updatedInvite = new InviteProto {Id = _storedInvite.Id};

            _mockTransaction = new Mock<ITransaction>(MockBehavior.Strict);
            _mockMemoryStoreClient = new Mock<IMemoryStoreClient>(MockBehavior.Strict);
            _mockMemoryStoreClient.Setup(client => client.CreateTransaction()).Returns(_mockTransaction.Object);
            _mockMemoryStoreClient.Setup(client => client.Dispose()).Verifiable();

            var memoryStoreClientManager = new Mock<IMemoryStoreClientManager<IMemoryStoreClient>>(MockBehavior.Strict);
            memoryStoreClientManager.Setup(manager => manager.GetClient()).Returns(_mockMemoryStoreClient.Object);
            _inviteService = new InviteServiceImpl(memoryStoreClientManager.Object);
        }

        [Test]
        public void ReturnInvalidArgumentWhenNoUpdatedInviteIsProvided()
        {
            // Check that having a non-empty updated invite is enforced by UpdateInvite.
            var context = Util.CreateFakeCallContext(SenderPlayerId, "");
            var exception =
                Assert.Throws<RpcException>(() => _inviteService.UpdateInvite(new UpdateInviteRequest(), context));
            Assert.That(exception.Message, Contains.Substring("non-empty updated invite"));
            Assert.AreEqual(StatusCode.InvalidArgument, exception.StatusCode);
        }

        [Test]
        public void ReturnInvalidArgumentWhenUpdatedInviteHasNoId()
        {
            // Check that having an updated invite with a valid id is enforced by UpdateInvite.
            var context = Util.CreateFakeCallContext(SenderPlayerId, "");
            var exception =
                Assert.Throws<RpcException>(() =>
                    _inviteService.UpdateInvite(new UpdateInviteRequest {UpdatedInvite = new InviteProto()}, context));
            Assert.That(exception.Message, Contains.Substring("updated invite with non-empty id"));
            Assert.AreEqual(StatusCode.InvalidArgument, exception.StatusCode);
        }

        [Test]
        public void ReturnEntryNotFoundWhenThereIsNoSuchInviteWithTheGivenId()
        {
            // Setup the client such that it will claim there is no such stored invite with the given id.
            _mockMemoryStoreClient.Setup(client => client.Get<InviteDataModel>(_updatedInvite.Id))
                .Returns((InviteDataModel) null);

            var context = Util.CreateFakeCallContext(SenderPlayerId, "");
            var request = new UpdateInviteRequest {UpdatedInvite = _updatedInvite};
            var exception = Assert.Throws<EntryNotFoundException>(() => _inviteService.UpdateInvite(request, context));
            Assert.AreEqual("No such invite with the given id found", exception.Message);
        }

        [Test]
        public void ReturnPermissionsDeniedWhenThePlayerIsNotInvolvedInTheInvite()
        {
            _mockMemoryStoreClient.Setup(client => client.Get<InviteDataModel>(_updatedInvite.Id))
                .Returns(_storedInvite);

            // Check that player involvement is enforced by UpdateInvite.
            var context = Util.CreateFakeCallContext("SomeoneElse", "");
            var request = new UpdateInviteRequest {UpdatedInvite = _updatedInvite};
            var exception = Assert.Throws<RpcException>(() => _inviteService.UpdateInvite(request, context));
            Assert.That(exception.Message, Contains.Substring("not involved"));
            Assert.AreEqual(StatusCode.PermissionDenied, exception.StatusCode);
        }

        [Test]
        public void ReturnUpdatedPartyIfExecutedSuccessfully()
        {
            // Setup the client such that it will successfully update the invite.
            var entriesUpdated = new List<Entry>();
            _mockMemoryStoreClient.Setup(client => client.Get<InviteDataModel>(_updatedInvite.Id))
                .Returns(_storedInvite);
            _mockTransaction.Setup(tr => tr.UpdateAll(It.IsAny<IEnumerable<Entry>>()))
                .Callback<IEnumerable<Entry>>(entries => entriesUpdated.AddRange(entries));
            _mockTransaction.Setup(tr => tr.Dispose());

            // Set the invite updates.
            var metadataUpdates = new Dictionary<string, string> {{"name", "online services"}, {"timestamp", ""}};
            _updatedInvite.Metadata.Add(metadataUpdates);
            _updatedInvite.CurrentStatus = InviteStatusProto.Declined;

            // Perform the update operation and extract the updated invite received as a response.
            var context = Util.CreateFakeCallContext(SenderPlayerId, "");
            var request = new UpdateInviteRequest {UpdatedInvite = _updatedInvite};
            var receivedInvite = _inviteService.UpdateInvite(request, context).Result.Invite;

            // Verify that the updated invite has the expected fields set.
            Assert.NotNull(receivedInvite);
            Assert.AreEqual(_storedInvite.Id, receivedInvite.Id);
            Assert.AreEqual(_storedInvite.SenderId, receivedInvite.SenderPlayerId);
            Assert.AreEqual(_storedInvite.ReceiverId, receivedInvite.ReceiverPlayerId);
            Assert.AreEqual(_storedInvite.PartyId, receivedInvite.PartyId);
            Assert.AreEqual(InviteStatusProto.Declined, receivedInvite.CurrentStatus);
            CollectionAssert.AreEquivalent(new Dictionary<string, string> {{"name", "online services"}},
                receivedInvite.Metadata);

            // Verify that the same updated invite was sent to the memory store.
            Assert.AreEqual(1, entriesUpdated.Count);
            Assert.IsInstanceOf<InviteDataModel>(entriesUpdated[0]);
            var updatedStoredInvite = (InviteDataModel) entriesUpdated[0];
            Assert.AreEqual(_storedInvite.Id, updatedStoredInvite.Id);
            Assert.AreEqual(_storedInvite.SenderId, updatedStoredInvite.SenderId);
            Assert.AreEqual(_storedInvite.ReceiverId, updatedStoredInvite.ReceiverId);
            Assert.AreEqual(_storedInvite.PartyId, updatedStoredInvite.PartyId);
            Assert.AreEqual(InviteStatusDataModel.Declined, updatedStoredInvite.CurrentStatus);
            CollectionAssert.AreEquivalent(new Dictionary<string, string> {{"name", "online services"}},
                updatedStoredInvite.Metadata);
        }
    }
}