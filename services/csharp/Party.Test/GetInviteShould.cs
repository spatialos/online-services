using System.Collections.Generic;
using Grpc.Core;
using Improbable.OnlineServices.Common.Analytics;
using Improbable.OnlineServices.Proto.Invite;
using MemoryStore;
using Moq;
using NUnit.Framework;
using InviteDataModel = Improbable.OnlineServices.DataModel.Party.Invite;
using InviteProto = Improbable.OnlineServices.Proto.Invite.Invite;

namespace Party.Test
{
    [TestFixture]
    public class GetInviteShould
    {
        private const string SenderPlayerId = "Jean-Claude Juncker";
        private const string ReceiverPlayerId = "UK";
        private const string PartyId = "EU";

        private static readonly IDictionary<string, string> _metadata = new Dictionary<string, string>
            {{"deadline", "29032019?"}};

        private InviteDataModel _storedInvite;
        private Mock<ITransaction> _mockTransaction;
        private Mock<IMemoryStoreClient> _mockMemoryStoreClient;
        private InviteServiceImpl _inviteService;

        [SetUp]
        public void SetUp()
        {
            _storedInvite = new InviteDataModel(SenderPlayerId, ReceiverPlayerId, PartyId, _metadata);
            _mockTransaction = new Mock<ITransaction>(MockBehavior.Strict);
            _mockMemoryStoreClient = new Mock<IMemoryStoreClient>(MockBehavior.Strict);
            _mockMemoryStoreClient.Setup(client => client.CreateTransaction()).Returns(_mockTransaction.Object);
            _mockMemoryStoreClient.Setup(client => client.Dispose()).Verifiable();

            var memoryStoreClientManager = new Mock<IMemoryStoreClientManager<IMemoryStoreClient>>(MockBehavior.Strict);
            memoryStoreClientManager.Setup(manager => manager.GetClient()).Returns(_mockMemoryStoreClient.Object);
            _inviteService = new InviteServiceImpl(memoryStoreClientManager.Object, new NullAnalyticsSender());
        }

        [Test]
        public void ReturnInvalidArgumentWhenNoInviteIdIsProvided()
        {
            // Check that having a non-empty invite id is enforced by GetInvite.
            var context = Util.CreateFakeCallContext(SenderPlayerId, "");
            var exception =
                Assert.ThrowsAsync<RpcException>(() => _inviteService.GetInvite(new GetInviteRequest(), context));
            Assert.That(exception.Message, Contains.Substring("non-empty invite id"));
            Assert.AreEqual(StatusCode.InvalidArgument, exception.StatusCode);
        }

        [Test]
        public void ReturnEntryNotFoundWhenInviteDoesNotExist()
        {
            // Setup the client such that it will claim there is no such invite with the given id.
            _mockMemoryStoreClient.Setup(client => client.GetAsync<InviteDataModel>(_storedInvite.Id))
                .ReturnsAsync((InviteDataModel) null);

            // Verify that the request has finished without any errors being thrown. 
            var context = Util.CreateFakeCallContext(SenderPlayerId, "");
            var request = new GetInviteRequest { InviteId = _storedInvite.Id };
            Assert.ThrowsAsync<EntryNotFoundException>(() => _inviteService.GetInvite(request, context));
        }

        [Test]
        public void ReturnPermissionsDeniedWhenThePlayerIsNotInvolvedInTheInvite()
        {
            // Setup the client such that it will claim the user making this request is not involved in the invite.
            _mockMemoryStoreClient.Setup(client => client.GetAsync<InviteDataModel>(_storedInvite.Id))
                .ReturnsAsync(_storedInvite);

            // Check that player involvement is enforced by GetInvite.
            var context = Util.CreateFakeCallContext("SomeoneElse", "");
            var request = new GetInviteRequest { InviteId = _storedInvite.Id };
            var exception = Assert.ThrowsAsync<RpcException>(() => _inviteService.GetInvite(request, context));
            Assert.AreEqual(StatusCode.PermissionDenied, exception.StatusCode);
        }

        [Test]
        public void ReturnInviteWhenFound()
        {
            // Setup the client such that it will claim there is an associated invite with the given id.
            _mockMemoryStoreClient.Setup(client => client.GetAsync<InviteDataModel>(_storedInvite.Id))
                .ReturnsAsync(_storedInvite);

            // Verify that the expected invite was returned as a response.
            var context = Util.CreateFakeCallContext(SenderPlayerId, "");
            var request = new GetInviteRequest { InviteId = _storedInvite.Id };
            var invite = _inviteService.GetInvite(request, context).Result.Invite;

            Assert.NotNull(invite);
            InviteComparator.AssertEquivalent(_storedInvite, invite);
        }
    }
}
