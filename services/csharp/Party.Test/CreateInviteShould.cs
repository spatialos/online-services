using System.Collections.Generic;
using Grpc.Core;
using Improbable.OnlineServices.Common.Analytics;
using Improbable.OnlineServices.DataModel;
using Improbable.OnlineServices.DataModel.Party;
using Improbable.OnlineServices.Proto.Invite;
using MemoryStore;
using Moq;
using NUnit.Framework;
using PartyDataModel = Improbable.OnlineServices.DataModel.Party.Party;
using InviteDataModel = Improbable.OnlineServices.DataModel.Party.Invite;

namespace Party.Test
{
    public class CreateInviteShould
    {
        private const string SenderPlayerId = "EU";
        private const string ReceiverPlayerId = "UK";
        private const string AnalyticsEventType = "player_invited_to_party";

        private static readonly IDictionary<string, string> _metadata = new Dictionary<string, string>
            {{"deadline", "29032019"}};

        private static PartyDataModel _party;

        private Mock<ITransaction> _mockTransaction;
        private Mock<IMemoryStoreClient> _mockMemoryStoreClient;
        private Mock<IAnalyticsSender> _mockAnalyticsSender;
        private InviteServiceImpl _inviteService;

        [SetUp]
        public void SetUp()
        {
            _party = new PartyDataModel(SenderPlayerId, "PIT", 6, 28);
            _party.AddPlayerToParty("FR", "PIT");

            _mockTransaction = new Mock<ITransaction>(MockBehavior.Strict);
            _mockMemoryStoreClient = new Mock<IMemoryStoreClient>(MockBehavior.Strict);
            _mockMemoryStoreClient.Setup(client => client.CreateTransaction()).Returns(_mockTransaction.Object);
            _mockMemoryStoreClient.Setup(client => client.Dispose()).Verifiable();
            _mockAnalyticsSender = new Mock<IAnalyticsSender>(MockBehavior.Strict);

            var memoryStoreClientManager = new Mock<IMemoryStoreClientManager<IMemoryStoreClient>>(MockBehavior.Strict);
            memoryStoreClientManager.Setup(manager => manager.GetClient()).Returns(_mockMemoryStoreClient.Object);
            _inviteService = new InviteServiceImpl(memoryStoreClientManager.Object, _mockAnalyticsSender.Object);
        }

        [Test]
        public void ReturnInvalidArgumentWhenNoReceiverPlayerIdIsProvided()
        {
            // Check that having a non-empty receiver player id is enforced by CreateInvite.
            var context = Util.CreateFakeCallContext(SenderPlayerId, "");
            var exception = Assert.ThrowsAsync<RpcException>(() =>
                _inviteService.CreateInvite(new CreateInviteRequest(), context));
            Assert.That(exception.Message, Contains.Substring("non-empty receiver"));
            Assert.AreEqual(StatusCode.InvalidArgument, exception.StatusCode);
        }

        [Test]
        public void ReturnFailedPreconditionIfTheSenderIsNotAMemberOfAnyParty()
        {
            // Setup the client such that it will claim that the sender is not a member of any party.
            _mockMemoryStoreClient.Setup(client => client.GetAsync<Member>(SenderPlayerId)).ReturnsAsync((Member) null);

            var context = Util.CreateFakeCallContext(SenderPlayerId, "");
            var exception = Assert.ThrowsAsync<RpcException>(() =>
                _inviteService.CreateInvite(new CreateInviteRequest { ReceiverPlayerId = ReceiverPlayerId }, context));
            Assert.That(exception.Message, Contains.Substring("not a member of any party"));
            Assert.AreEqual(StatusCode.FailedPrecondition, exception.StatusCode);
        }

        [Test]
        public void ReturnFailedPreconditionIfThePlayerHasMeanwhileLeftTheParty()
        {
            // Setup the client such that it will claim that initially the sender was a member of a specific party but
            // has meanwhile been removed from the party, right between the two Get operations.
            var member = _party.GetMember(SenderPlayerId);
            _party.RemovePlayerFromParty(SenderPlayerId);
            _mockMemoryStoreClient.Setup(client => client.GetAsync<Member>(SenderPlayerId)).ReturnsAsync(member);
            _mockMemoryStoreClient.Setup(client => client.GetAsync<PartyDataModel>(member.PartyId)).ReturnsAsync(_party);

            var context = Util.CreateFakeCallContext(SenderPlayerId, "");
            var exception = Assert.ThrowsAsync<RpcException>(() =>
                _inviteService.CreateInvite(new CreateInviteRequest { ReceiverPlayerId = ReceiverPlayerId }, context));
            Assert.That(exception.Message, Contains.Substring("not a member of any party"));
            Assert.AreEqual(StatusCode.FailedPrecondition, exception.StatusCode);
        }

        [Test]
        public void ReturnFailedPreconditionWhenTheReceiverIsAlreadyAMemberOfTheParty()
        {
            // Setup the client such that it will claim that the receiver is already a member of the party.
            _party.AddPlayerToParty(ReceiverPlayerId, "PIT");
            _mockMemoryStoreClient.Setup(client => client.GetAsync<Member>(ReceiverPlayerId))
                .ReturnsAsync(_party.GetMember(ReceiverPlayerId));
            _mockMemoryStoreClient.Setup(client => client.GetAsync<PartyDataModel>(_party.Id)).ReturnsAsync(_party);

            var context = Util.CreateFakeCallContext(ReceiverPlayerId, "");
            var exception = Assert.ThrowsAsync<RpcException>(() =>
                _inviteService.CreateInvite(new CreateInviteRequest { ReceiverPlayerId = ReceiverPlayerId }, context));
            Assert.That(exception.Message, Contains.Substring("already a member"));
            Assert.AreEqual(StatusCode.FailedPrecondition, exception.StatusCode);
        }

        [Test]
        public void ReturnInviteIdWhenCreatingInviteSuccessfullyWithNoExistingPlayerInvites()
        {
            var expectedCreatedInvite = new InviteDataModel(SenderPlayerId, ReceiverPlayerId, _party.Id, _metadata);

            // Setup the client so that it will successfully create an invite.
            _mockMemoryStoreClient.Setup(client => client.GetAsync<Member>(SenderPlayerId))
                .ReturnsAsync(_party.GetMember(SenderPlayerId));
            _mockMemoryStoreClient.Setup(client => client.GetAsync<PartyDataModel>(_party.Id)).ReturnsAsync(_party);
            _mockMemoryStoreClient.Setup(client => client.GetAsync<InviteDataModel>(expectedCreatedInvite.Id))
                .ReturnsAsync((InviteDataModel) null);
            _mockMemoryStoreClient.Setup(client => client.GetAsync<PlayerInvites>(SenderPlayerId))
                .ReturnsAsync((PlayerInvites) null);
            _mockMemoryStoreClient.Setup(client => client.GetAsync<PlayerInvites>(ReceiverPlayerId))
                .ReturnsAsync((PlayerInvites) null);
            SetupAnalyticsExpectation(expectedCreatedInvite);

            var entriesCreated = new List<Entry>();
            var entriesUpdated = new List<Entry>();
            _mockTransaction.Setup(tr => tr.CreateAll(It.IsAny<IEnumerable<Entry>>()))
                .Callback<IEnumerable<Entry>>(entries => entriesCreated.AddRange(entries));
            _mockTransaction.Setup(tr => tr.UpdateAll(It.IsAny<IEnumerable<Entry>>()))
                .Callback<IEnumerable<Entry>>(entries => entriesUpdated.AddRange(entries));
            _mockTransaction.Setup(tr => tr.Dispose());

            // Check that the RPC has completely successfully and that an empty response was returned.
            var context = Util.CreateFakeCallContext(SenderPlayerId, "");
            var request = new CreateInviteRequest { ReceiverPlayerId = ReceiverPlayerId };
            request.Metadata.Add(_metadata);
            var response = _inviteService.CreateInvite(request, context).Result;
            Assert.AreEqual(expectedCreatedInvite.Id, response.InviteId);

            // Check that an Invite entry has been created. PlayerInvites for the sender and receiver should have also
            // been created.
            Assert.AreEqual(3, entriesCreated.Count);
            Assert.IsInstanceOf<InviteDataModel>(entriesCreated[0]);
            Assert.IsInstanceOf<PlayerInvites>(entriesCreated[1]);

            var inviteCreated = (InviteDataModel) entriesCreated[0];
            Assert.AreEqual(SenderPlayerId, inviteCreated.SenderId);
            Assert.AreEqual(ReceiverPlayerId, inviteCreated.ReceiverId);
            Assert.AreEqual(_party.Id, inviteCreated.PartyId);
            Assert.AreEqual(InviteDataModel.Status.Pending, inviteCreated.CurrentStatus);
            CollectionAssert.AreEquivalent(_metadata, inviteCreated.Metadata);

            var senderPlayerInvites = (PlayerInvites) entriesCreated[1];
            Assert.AreEqual(SenderPlayerId, senderPlayerInvites.Id);
            Assert.That(senderPlayerInvites.OutboundInviteIds, Contains.Item(expectedCreatedInvite.Id));

            var receiverPlayerInvites = (PlayerInvites) entriesCreated[2];
            Assert.AreEqual(ReceiverPlayerId, receiverPlayerInvites.Id);
            Assert.That(receiverPlayerInvites.InboundInviteIds, Contains.Item(expectedCreatedInvite.Id));

            CollectionAssert.IsEmpty(entriesUpdated);

            _mockAnalyticsSender.VerifyAll();
        }

        [Test]
        public void ReturnInviteIdWhenCreatingInviteSuccessfullyWithExistingPlayerInvites()
        {
            var expectedCreatedInvite = new InviteDataModel(SenderPlayerId, ReceiverPlayerId, _party.Id, _metadata);

            // Setup the client so that it will successfully create an invite.
            _mockMemoryStoreClient.Setup(client => client.GetAsync<Member>(SenderPlayerId))
                .ReturnsAsync(_party.GetMember(SenderPlayerId));
            _mockMemoryStoreClient.Setup(client => client.GetAsync<PartyDataModel>(_party.Id)).ReturnsAsync(_party);
            _mockMemoryStoreClient.Setup(client => client.GetAsync<InviteDataModel>(expectedCreatedInvite.Id))
                .ReturnsAsync((InviteDataModel) null);
            _mockMemoryStoreClient.Setup(client => client.GetAsync<PlayerInvites>(SenderPlayerId))
                .ReturnsAsync(new PlayerInvites(SenderPlayerId));
            _mockMemoryStoreClient.Setup(client => client.GetAsync<PlayerInvites>(ReceiverPlayerId))
                .ReturnsAsync(new PlayerInvites(ReceiverPlayerId));

            var entriesCreated = new List<Entry>();
            var entriesUpdated = new List<Entry>();
            _mockTransaction.Setup(tr => tr.CreateAll(It.IsAny<IEnumerable<Entry>>()))
                .Callback<IEnumerable<Entry>>(entries => entriesCreated.AddRange(entries));
            _mockTransaction.Setup(tr => tr.UpdateAll(It.IsAny<IEnumerable<Entry>>()))
                .Callback<IEnumerable<Entry>>(entries => entriesUpdated.AddRange(entries));
            _mockTransaction.Setup(tr => tr.Dispose());
            SetupAnalyticsExpectation(expectedCreatedInvite);

            // Check that the RPC has completely successfully and that an empty response was returned.
            var context = Util.CreateFakeCallContext(SenderPlayerId, "");
            var request = new CreateInviteRequest { ReceiverPlayerId = ReceiverPlayerId };
            request.Metadata.Add(_metadata);
            var response = _inviteService.CreateInvite(request, context).Result;
            Assert.AreEqual(expectedCreatedInvite.Id, response.InviteId);

            // Check that an Invite entry has been created.
            Assert.AreEqual(1, entriesCreated.Count);
            Assert.IsInstanceOf<InviteDataModel>(entriesCreated[0]);

            var inviteCreated = (InviteDataModel) entriesCreated[0];
            Assert.AreEqual(SenderPlayerId, inviteCreated.SenderId);
            Assert.AreEqual(ReceiverPlayerId, inviteCreated.ReceiverId);
            Assert.AreEqual(_party.Id, inviteCreated.PartyId);
            Assert.AreEqual(InviteDataModel.Status.Pending, inviteCreated.CurrentStatus);
            CollectionAssert.AreEquivalent(_metadata, inviteCreated.Metadata);

            // Verify that the player invites for the sender and receiver have been updated since they already existed
            // in the memory store.
            Assert.AreEqual(2, entriesUpdated.Count);
            var senderPlayerInvites = (PlayerInvites) entriesUpdated[0];
            Assert.AreEqual(SenderPlayerId, senderPlayerInvites.Id);
            Assert.That(senderPlayerInvites.OutboundInviteIds, Contains.Item(expectedCreatedInvite.Id));

            Assert.IsInstanceOf<PlayerInvites>(entriesUpdated[1]);
            var receiverPlayerInvites = (PlayerInvites) entriesUpdated[1];
            Assert.AreEqual(ReceiverPlayerId, receiverPlayerInvites.Id);
            Assert.That(receiverPlayerInvites.InboundInviteIds, Contains.Item(expectedCreatedInvite.Id));

            _mockAnalyticsSender.VerifyAll();
        }

        private void SetupAnalyticsExpectation(InviteDataModel expectedCreatedInvite)
        {
            _mockAnalyticsSender.Setup(sender =>
                                           sender.Send(AnalyticsConstants.InviteClass, AnalyticsEventType,
                                                       new Dictionary<string, string> {
                                                           { AnalyticsConstants.PlayerId, ReceiverPlayerId },
                                                           { AnalyticsConstants.PartyId, _party.Id },
                                                           { AnalyticsConstants.PlayerIdInviter, SenderPlayerId },
                                                           { AnalyticsConstants.InviteId, expectedCreatedInvite.Id }
                                                       }, ReceiverPlayerId));
        }
    }
}
