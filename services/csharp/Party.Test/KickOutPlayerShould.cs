using System.Collections.Generic;
using Grpc.Core;
using Improbable.OnlineServices.Common.Analytics;
using Improbable.OnlineServices.DataModel;
using Improbable.OnlineServices.DataModel.Party;
using Improbable.OnlineServices.Proto.Party;
using MemoryStore;
using Moq;
using NUnit.Framework;
using PartyDataModel = Improbable.OnlineServices.DataModel.Party.Party;

namespace Party.Test
{
    [TestFixture]
    public class KickOutPlayerShould
    {
        private const string TestInitiatorPlayerId = "Frodo";
        private const string TestEvictedPlayerId = "Sam";
        private const string Pit = "PIT";
        private const string AnalyticsEventType = "player_kicked_from_party";

        private static readonly KickOutPlayerRequest _testKickOutRequest = new KickOutPlayerRequest
        {
            EvictedPlayerId = TestEvictedPlayerId
        };

        private static PartyDataModel _testParty;

        private Mock<ITransaction> _mockTransaction;
        private Mock<IMemoryStoreClient> _mockMemoryStoreClient;
        private Mock<IAnalyticsSender> _mockAnalyticsSender;
        private PartyServiceImpl _partyService;

        [SetUp]
        public void SetUp()
        {
            _testParty = new PartyDataModel(TestInitiatorPlayerId, Pit);
            _testParty.AddPlayerToParty(TestEvictedPlayerId, Pit);

            _mockTransaction = new Mock<ITransaction>(MockBehavior.Strict);
            _mockMemoryStoreClient = new Mock<IMemoryStoreClient>(MockBehavior.Strict);
            _mockMemoryStoreClient.Setup(client => client.Dispose()).Verifiable();
            _mockMemoryStoreClient.Setup(client => client.CreateTransaction()).Returns(_mockTransaction.Object);
            _mockAnalyticsSender = new Mock<IAnalyticsSender>(MockBehavior.Strict);

            var memoryStoreClientManager = new Mock<IMemoryStoreClientManager<IMemoryStoreClient>>(MockBehavior.Strict);
            memoryStoreClientManager.Setup(manager => manager.GetClient()).Returns(_mockMemoryStoreClient.Object);
            _partyService = new PartyServiceImpl(memoryStoreClientManager.Object, _mockAnalyticsSender.Object);
        }

        [Test]
        public void ReturnInvalidArgumentWhenNoEvictedPlayerIdIsProvided()
        {
            // Check that having a non-empty evicted player id is enforced by KickOutPlayer.
            var context = Util.CreateFakeCallContext(TestInitiatorPlayerId, Pit);
            var exception =
                Assert.ThrowsAsync<RpcException>(() => _partyService.KickOutPlayer(new KickOutPlayerRequest(), context));
            Assert.That(exception.Message, Contains.Substring("requires a non-empty evicted"));
            Assert.AreEqual(StatusCode.InvalidArgument, exception.StatusCode);
        }

        [Test]
        public void DelegateCallToLeavePartyWhenInitiatorIsKickingOutHimself()
        {
            // Setup the client such that it will successfully let the initiator kick himself out of the party. In order
            // to test the implementation has reached LeaveParty, the initiator should be the last member of the party
            // such that an exception would be thrown. This flow would never occur on a normal KickOut operation.
            _testParty.RemovePlayerFromParty(TestEvictedPlayerId);
            _mockMemoryStoreClient.Setup(client => client.GetAsync<Member>(TestInitiatorPlayerId))
                .ReturnsAsync(_testParty.GetMember(TestInitiatorPlayerId));
            _mockMemoryStoreClient.Setup(client => client.GetAsync<PartyDataModel>(_testParty.Id))
                .ReturnsAsync(_testParty);

            // Verify that an exception was thrown, preventing the player from leaving the party.
            var context = Util.CreateFakeCallContext(TestInitiatorPlayerId, Pit);
            var selfKickOutRequest = new KickOutPlayerRequest { EvictedPlayerId = TestInitiatorPlayerId };
            var exception = Assert.ThrowsAsync<RpcException>(() => _partyService.KickOutPlayer(selfKickOutRequest, context));
            Assert.AreEqual(StatusCode.FailedPrecondition, exception.StatusCode);
            Assert.That(exception.Message, Contains.Substring("Cannot remove player if last member"));
        }

        [Test]
        public void ReturnNotFoundWhenInitiatorIsNotAMemberOfAnyParty()
        {
            // Setup the client such that it will claim that the initiator player id is not a member of any party.
            _mockMemoryStoreClient.Setup(client => client.GetAsync<Member>(TestInitiatorPlayerId)).ReturnsAsync((Member) null);
            _mockMemoryStoreClient.Setup(client => client.GetAsync<Member>(TestEvictedPlayerId))
                .ReturnsAsync(_testParty.GetMember(TestEvictedPlayerId));

            // Check that the kick-out request will throw an exception. 
            var context = Util.CreateFakeCallContext(TestInitiatorPlayerId, Pit);
            var exception =
                Assert.ThrowsAsync<RpcException>(() => _partyService.KickOutPlayer(_testKickOutRequest, context));
            Assert.That(exception.Message, Contains.Substring("initiator player is not a member of any party"));
            Assert.AreEqual(StatusCode.NotFound, exception.StatusCode);
        }

        [Test]
        public void ReturnEarlyWhenEvictedIsNotAMemberOfAnyParty()
        {
            // Setup the client such that it will claim that the evicted player id is not member of any party.
            _mockMemoryStoreClient.Setup(client => client.GetAsync<Member>(TestInitiatorPlayerId))
                .ReturnsAsync(_testParty.GetLeader);
            _mockMemoryStoreClient.Setup(client => client.GetAsync<Member>(TestEvictedPlayerId)).ReturnsAsync((Member) null);

            // Check that an empty response has been returned.
            var context = Util.CreateFakeCallContext(TestInitiatorPlayerId, Pit);
            var response = _partyService.KickOutPlayer(_testKickOutRequest, context).Result;
            Assert.AreEqual(new KickOutPlayerResponse(), response);
        }

        [Test]
        public void ReturnNotFoundWhenThePartyNoLongerExists()
        {
            // Setup the client such that it will claim that the party has been deleted meanwhile.
            _mockMemoryStoreClient.Setup(client => client.GetAsync<Member>(TestInitiatorPlayerId))
                .ReturnsAsync(_testParty.GetLeader);
            _mockMemoryStoreClient.Setup(client => client.GetAsync<Member>(TestEvictedPlayerId))
                .ReturnsAsync(_testParty.GetMember(TestEvictedPlayerId));
            _mockMemoryStoreClient.Setup(client => client.GetAsync<PartyDataModel>(_testParty.Id))
                .ReturnsAsync((PartyDataModel) null);

            // Check that the kick-out request will throw an exception. 
            var context = Util.CreateFakeCallContext(TestInitiatorPlayerId, Pit);
            var exception =
                Assert.ThrowsAsync<RpcException>(() => _partyService.KickOutPlayer(_testKickOutRequest, context));
            Assert.That(exception.Message, Contains.Substring("party no longer exists"));
            Assert.AreEqual(StatusCode.NotFound, exception.StatusCode);
        }

        [Test]
        public void ReturnPermissionDeniedWhenInitiatorIsNotTheLeader()
        {
            // Setup the client such that it will claim that the initiator is not the leader of the party.
            _testParty.AddPlayerToParty("SomeoneNew", Pit);
            _testParty.UpdatePartyLeader("SomeoneNew");

            _mockMemoryStoreClient.Setup(client => client.GetAsync<Member>(TestInitiatorPlayerId))
                .ReturnsAsync(_testParty.GetMember(TestInitiatorPlayerId));
            _mockMemoryStoreClient.Setup(client => client.GetAsync<Member>(TestEvictedPlayerId))
                .ReturnsAsync(_testParty.GetMember(TestEvictedPlayerId));
            _mockMemoryStoreClient.Setup(client => client.GetAsync<PartyDataModel>(_testParty.Id))
                .ReturnsAsync(_testParty);

            // Check that the kick-out request will throw an exception. 
            var context = Util.CreateFakeCallContext(TestInitiatorPlayerId, Pit);
            var exception =
                Assert.ThrowsAsync<RpcException>(() => _partyService.KickOutPlayer(_testKickOutRequest, context));
            Assert.That(exception.Message, Contains.Substring("initiator is not the leader"));
            Assert.AreEqual(StatusCode.PermissionDenied, exception.StatusCode);
        }

        [Test]
        public void ReturnPermissionDeniedWhenInitiationAndEvictedAreNotMembersOfTheSameParty()
        {
            // Setup the client such that it will claim that the initiator and evicted players are not members of the 
            // same party.
            _testParty.RemovePlayerFromParty(TestEvictedPlayerId);

            _mockMemoryStoreClient.Setup(client => client.GetAsync<Member>(TestInitiatorPlayerId))
                .ReturnsAsync(_testParty.GetLeader);
            _mockMemoryStoreClient.Setup(client => client.GetAsync<Member>(TestEvictedPlayerId))
                .ReturnsAsync(new Member(TestEvictedPlayerId, "AnotherParty"));
            _mockMemoryStoreClient.Setup(client => client.GetAsync<PartyDataModel>(_testParty.Id))
                .ReturnsAsync(_testParty);

            // Check that the kick-out request will throw an exception. 
            var context = Util.CreateFakeCallContext(TestInitiatorPlayerId, Pit);
            var exception =
                Assert.ThrowsAsync<RpcException>(() => _partyService.KickOutPlayer(_testKickOutRequest, context));
            Assert.That(exception.Message, Contains.Substring("players are not members of the same"));
            Assert.AreEqual(StatusCode.PermissionDenied, exception.StatusCode);
        }

        [Test]
        public void ReturnEarlyWhenThePlayerHasAlreadyLeftTheParty()
        {
            // Setup the client such that it will claim that the evicted has already been kicked out of the party.
            var evicted = _testParty.GetMember(TestEvictedPlayerId);
            _testParty.RemovePlayerFromParty(TestEvictedPlayerId);

            _mockMemoryStoreClient.Setup(client => client.GetAsync<Member>(TestInitiatorPlayerId))
                .ReturnsAsync(_testParty.GetLeader);
            _mockMemoryStoreClient.Setup(client => client.GetAsync<Member>(TestEvictedPlayerId))
                .ReturnsAsync(evicted);
            _mockMemoryStoreClient.Setup(client => client.GetAsync<PartyDataModel>(_testParty.Id))
                .ReturnsAsync(_testParty);

            // Check that an empty response has been returned as a result.
            var context = Util.CreateFakeCallContext(TestInitiatorPlayerId, Pit);
            var response = _partyService.KickOutPlayer(_testKickOutRequest, context).Result;
            Assert.AreEqual(new KickOutPlayerResponse(), response);
        }

        [Test]
        public void ReturnEmptyResponseOnSuccessfulKickOut()
        {
            // Setup the client such that it will successfully complete the kick-out op.
            var entriesDeleted = new List<Entry>();
            var entriesUpdated = new List<Entry>();
            _mockMemoryStoreClient.Setup(client => client.GetAsync<Member>(TestInitiatorPlayerId))
                .ReturnsAsync(_testParty.GetMember(TestInitiatorPlayerId));
            _mockMemoryStoreClient.Setup(client => client.GetAsync<Member>(TestEvictedPlayerId))
                .ReturnsAsync(_testParty.GetMember(TestEvictedPlayerId));
            _mockMemoryStoreClient.Setup(client => client.GetAsync<PartyDataModel>(_testParty.Id))
                .ReturnsAsync(_testParty);
            _mockTransaction.Setup(tr => tr.DeleteAll(It.IsAny<IEnumerable<Entry>>()))
                .Callback<IEnumerable<Entry>>(entries => entriesDeleted.AddRange(entries));
            _mockTransaction.Setup(tr => tr.UpdateAll(It.IsAny<IEnumerable<Entry>>()))
                .Callback<IEnumerable<Entry>>(entries => entriesUpdated.AddRange(entries));
            _mockTransaction.Setup(tr => tr.Dispose());
            _mockAnalyticsSender.Setup(
                sender => sender.Send(AnalyticsConstants.PartyClass, AnalyticsEventType,
                                      new Dictionary<string, string> {
                                          { AnalyticsConstants.PlayerId, TestEvictedPlayerId },
                                          { AnalyticsConstants.PartyId, _testParty.Id },
                                          { AnalyticsConstants.PlayerIdKicker, TestInitiatorPlayerId }
                                      }, TestEvictedPlayerId));

            // Check that that the operation has completed successfully without any exceptions being thrown. Verify that
            // an empty response was returned.
            var context = Util.CreateFakeCallContext(TestInitiatorPlayerId, Pit);
            var response = _partyService.KickOutPlayer(_testKickOutRequest, context).Result;
            Assert.AreEqual(new KickOutPlayerResponse(), response);
            _mockMemoryStoreClient.Verify();

            // Verify that the expected entries have been deleted/updated.
            Assert.AreEqual(1, entriesDeleted.Count);
            Assert.IsInstanceOf<Member>(entriesDeleted[0]);

            var member = (Member) entriesDeleted[0];
            Assert.AreEqual(TestEvictedPlayerId, member.Id);
            Assert.AreEqual(_testParty.Id, member.PartyId);

            Assert.AreEqual(1, entriesUpdated.Count);
            Assert.IsInstanceOf<PartyDataModel>(entriesUpdated[0]);
            var updatedParty = (PartyDataModel) entriesUpdated[0];
            Assert.AreEqual(_testParty.Id, updatedParty.Id);
            Assert.IsNull(updatedParty.GetMember(TestEvictedPlayerId));

            _mockAnalyticsSender.VerifyAll();
        }
    }
}
