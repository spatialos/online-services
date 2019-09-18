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
    public class LeavePartyShould
    {
        private const string TestLeaderId = "Gridelwald2018";
        private const string TestPlayerId = "Credence2018";
        private const string Pit = "PIT";
        private const string AnalyticsEventType = "player_left_party";

        private static PartyDataModel _testParty;

        private Mock<ITransaction> _mockTransaction;
        private Mock<IMemoryStoreClient> _mockMemoryStoreClient;
        private Mock<IAnalyticsSender> _mockAnalyticsSender;
        private PartyServiceImpl _partyService;

        [SetUp]
        public void SetUp()
        {
            _testParty = new PartyDataModel(TestLeaderId, Pit);
            _testParty.AddPlayerToParty(TestPlayerId, Pit);

            _mockTransaction = new Mock<ITransaction>(MockBehavior.Strict);
            _mockTransaction.Setup(tr => tr.Dispose());
            _mockMemoryStoreClient = new Mock<IMemoryStoreClient>(MockBehavior.Strict);
            _mockMemoryStoreClient.Setup(client => client.CreateTransaction()).Returns(_mockTransaction.Object);
            _mockMemoryStoreClient.Setup(client => client.Dispose()).Verifiable();
            _mockAnalyticsSender = new Mock<IAnalyticsSender>(MockBehavior.Strict);

            var memoryStoreClientManager = new Mock<IMemoryStoreClientManager<IMemoryStoreClient>>(MockBehavior.Strict);
            memoryStoreClientManager.Setup(manager => manager.GetClient()).Returns(_mockMemoryStoreClient.Object);
            _partyService = new PartyServiceImpl(memoryStoreClientManager.Object, _mockAnalyticsSender.Object);
        }

        [Test]
        public void ReturnEarlyWhenThePlayerIsNotAMemberOfAnyParty()
        {
            // Setup the client such that it will claim that TestPlayer is not a member of any party.
            _mockMemoryStoreClient.Setup(client => client.GetAsync<Member>(TestPlayerId)).ReturnsAsync((Member) null);

            // Check that an empty response has been returned.
            var context = Util.CreateFakeCallContext(TestPlayerId, Pit);
            var response = _partyService.LeaveParty(new LeavePartyRequest(), context).Result;
            Assert.AreEqual(new LeavePartyResponse(), response);
        }

        [Test]
        public void ReturnNotFoundWhenThePartyNoLongerExists()
        {
            // Setup the client such that it will claim that party has been deleted meanwhile.
            _mockMemoryStoreClient.Setup(client => client.GetAsync<Member>(TestPlayerId))
                .ReturnsAsync(_testParty.GetMember(TestPlayerId));
            _mockMemoryStoreClient.Setup(client => client.GetAsync<PartyDataModel>(_testParty.Id))
                .ReturnsAsync((PartyDataModel) null);

            // Check that an exception was thrown when trying to leave the party.
            var context = Util.CreateFakeCallContext(TestPlayerId, Pit);
            var exception = Assert.ThrowsAsync<RpcException>(() =>
                _partyService.LeaveParty(new LeavePartyRequest(), context));
            Assert.That(exception.Message, Contains.Substring("no longer exists"));
            Assert.AreEqual(StatusCode.NotFound, exception.StatusCode);
        }

        [Test]
        public void ReturnEarlyWhenThePlayerHasAlreadyLeftTheParty()
        {
            // Setup the client such that it will claim that between the two gets the player has already left the party.
            var member = _testParty.GetMember(TestPlayerId);
            _testParty.RemovePlayerFromParty(TestPlayerId);

            _mockMemoryStoreClient.Setup(client => client.GetAsync<Member>(TestPlayerId)).ReturnsAsync(member);
            _mockMemoryStoreClient.Setup(client => client.GetAsync<PartyDataModel>(_testParty.Id)).ReturnsAsync(_testParty);

            // Check that an empty response has been returned.
            var context = Util.CreateFakeCallContext(TestPlayerId, Pit);
            var response = _partyService.LeaveParty(new LeavePartyRequest(), context).Result;
            Assert.AreEqual(new LeavePartyResponse(), response);
        }

        [Test]
        public void ReturnFailedPreconditionIfTryingToLeavePartyAsItsLastMember()
        {
            // Setup the client such that it will claim that the player is the last member of the party.
            _testParty.RemovePlayerFromParty(TestPlayerId);
            _mockMemoryStoreClient.Setup(client => client.GetAsync<Member>(TestLeaderId))
                .ReturnsAsync(_testParty.GetLeader);
            _mockMemoryStoreClient.Setup(client => client.GetAsync<PartyDataModel>(_testParty.Id))
                .ReturnsAsync(_testParty);

            // Verify that an exception was thrown, preventing the player from leaving the party.
            var context = Util.CreateFakeCallContext(TestLeaderId, Pit);
            var exception =
                Assert.ThrowsAsync<RpcException>(() => _partyService.LeaveParty(new LeavePartyRequest(), context));
            Assert.AreEqual(StatusCode.FailedPrecondition, exception.StatusCode);
            Assert.That(exception.Message, Contains.Substring("Cannot remove player if last member"));
        }

        [Test]
        public void ReturnEmptyResponseWhenSuccessfullyRemovingPlayer()
        {
            // Setup the client such that it will successfully delete the player from the party. 
            var entriesDeleted = new List<Entry>();
            var entriesUpdated = new List<Entry>();
            _mockMemoryStoreClient.Setup(client => client.GetAsync<Member>(TestLeaderId))
                                  .ReturnsAsync(_testParty.GetLeader);
            _mockMemoryStoreClient.Setup(client => client.GetAsync<PartyDataModel>(_testParty.Id))
                                  .ReturnsAsync(_testParty);
            _mockTransaction.Setup(tr => tr.DeleteAll(It.IsAny<IEnumerable<Entry>>()))
                            .Callback<IEnumerable<Entry>>(entries => entriesDeleted.AddRange(entries));
            _mockTransaction.Setup(tr => tr.UpdateAll(It.IsAny<IEnumerable<Entry>>()))
                            .Callback<IEnumerable<Entry>>(entries => entriesUpdated.AddRange(entries));
            _mockTransaction.Setup(tr => tr.Dispose());
            _mockAnalyticsSender.Setup(
                sender => sender.Send(AnalyticsConstants.PartyClass, AnalyticsEventType, new Dictionary<string, string> {
                    { AnalyticsConstants.PlayerId, TestLeaderId },
                    { AnalyticsConstants.PartyId, _testParty.Id }
                }, TestLeaderId));

            // Check that the leave op has successfully completed without any exceptions being thrown.
            var context = Util.CreateFakeCallContext(TestLeaderId, Pit);
            var response = _partyService.LeaveParty(new LeavePartyRequest(), context).Result;
            Assert.AreEqual(response, new LeavePartyResponse());

            // Verify that the expected entries have been deleted/updated.
            Assert.AreEqual(1, entriesDeleted.Count);
            Assert.IsInstanceOf<Member>(entriesDeleted[0]);

            var member = (Member) entriesDeleted[0];
            Assert.AreEqual(TestLeaderId, member.Id);
            Assert.AreEqual(_testParty.Id, member.PartyId);

            Assert.AreEqual(1, entriesUpdated.Count);
            Assert.IsInstanceOf<PartyDataModel>(entriesUpdated[0]);

            // Verify that the leave resulted in a different leader being assigned.
            var updatedParty = (PartyDataModel) entriesUpdated[0];
            Assert.AreEqual(_testParty.Id, updatedParty.Id);
            Assert.IsNull(updatedParty.GetMember(TestLeaderId));
            Assert.AreEqual(TestPlayerId, updatedParty.GetLeader().Id);

            _mockAnalyticsSender.VerifyAll();
        }
    }
}
