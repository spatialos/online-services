using System.Collections.Generic;
using System.Linq;
using Grpc.Core;
using Improbable.OnlineServices.Common.Analytics;
using Improbable.OnlineServices.DataModel;
using Improbable.OnlineServices.DataModel.Party;
using Improbable.OnlineServices.Proto.Party;
using MemoryStore;
using Moq;
using NUnit.Framework;
using PartyDataModel = Improbable.OnlineServices.DataModel.Party.Party;
using PartyProto = Improbable.OnlineServices.Proto.Party.Party;

namespace Party.Test
{
    [TestFixture]
    public class DeletePartyShould
    {
        private const string TestPlayerId = "Gridelwald2018";
        private const string Pit = "PIT";
        private const string AnalyticsEventTypePartyDeleted = "player_cancelled_party";
        private const string AnalyticsEventTypePartyDeletedMemberKicked = "player_left_cancelled_party";

        private static readonly PartyDataModel _testParty = new PartyDataModel(TestPlayerId, Pit, 2, 5);

        private static readonly Member _testMember = new Member(TestPlayerId, _testParty.Id);

        private Mock<ITransaction> _mockTransaction;
        private Mock<IMemoryStoreClient> _mockMemoryStoreClient;
        private Mock<IAnalyticsSender> _mockAnalyticsSender;
        private PartyServiceImpl _partyService;

        [SetUp]
        public void SetUp()
        {
            _mockTransaction = new Mock<ITransaction>(MockBehavior.Strict);
            _mockTransaction.Setup(tr => tr.Dispose());
            _mockMemoryStoreClient = new Mock<IMemoryStoreClient>(MockBehavior.Strict);
            _mockMemoryStoreClient.Setup(client => client.Dispose()).Verifiable();
            _mockMemoryStoreClient.Setup(client => client.CreateTransaction()).Returns(_mockTransaction.Object);
            _mockAnalyticsSender = new Mock<IAnalyticsSender>(MockBehavior.Strict);
            var memoryStoreClientManager = new Mock<IMemoryStoreClientManager<IMemoryStoreClient>>(MockBehavior.Strict);
            memoryStoreClientManager.Setup(manager => manager.GetClient()).Returns(_mockMemoryStoreClient.Object);
            _partyService = new PartyServiceImpl(memoryStoreClientManager.Object, _mockAnalyticsSender.Object);
        }

        [Test]
        public void ReturnNotFoundWhenThereIsNoPartyAssociatedToThePlayer()
        {
            // Setup the client such that will claim that TestPlayer is not a member of any party. 
            _mockMemoryStoreClient.Setup(client => client.GetAsync<Member>(TestPlayerId))
                .ReturnsAsync((Member) null);

            // Check that an RpcException was thrown signalising that the player wasn't associated to any party.
            var context = Util.CreateFakeCallContext(TestPlayerId, Pit);
            var exception = Assert.ThrowsAsync<RpcException>(() =>
                _partyService.DeleteParty(new DeletePartyRequest(), context));
            Assert.That(exception.Message, Contains.Substring("not a member of any party"));
            Assert.AreEqual(StatusCode.NotFound, exception.StatusCode);
        }

        [Test]
        public void ReturnPermissionDeniedWhenThePlayerIsNotTheLeaderOfTheParty()
        {
            // Setup the client so that it will return a party which doesn't have the player set as its leader.
            var party = new PartyDataModel(_testParty);
            party.AddPlayerToParty("SomeoneElse", Pit);
            party.UpdatePartyLeader("SomeoneElse");

            _mockMemoryStoreClient.Setup(client => client.GetAsync<Member>(TestPlayerId))
                .ReturnsAsync(_testMember);
            _mockMemoryStoreClient.Setup(client => client.GetAsync<PartyDataModel>(_testMember.PartyId))
                .ReturnsAsync(party);

            // Check that an RpcException was thrown signalising that the player doesn't have the necessary permissions
            // to delete the party.
            var context = Util.CreateFakeCallContext(TestPlayerId, Pit);
            var exception = Assert.ThrowsAsync<RpcException>(() =>
                _partyService.DeleteParty(new DeletePartyRequest(), context));
            Assert.That(exception.Message, Contains.Substring("needs to be the leader"));
            Assert.AreEqual(StatusCode.PermissionDenied, exception.StatusCode);
        }

        [Test]
        public void ThrowTransactionAbortedExceptionIfSomeMemberWasNotFound()
        {
            // Setup the client so that it will throw an EntryNotFoundException when writing the information in the
            // memory store.
            _mockMemoryStoreClient.Setup(client => client.GetAsync<Member>(TestPlayerId))
                .ReturnsAsync(_testMember);
            _mockMemoryStoreClient.Setup(client => client.GetAsync<PartyDataModel>(_testMember.PartyId))
                .ReturnsAsync(_testParty);
            _mockTransaction.Setup(tr => tr.DeleteAll(It.IsAny<IEnumerable<Entry>>()));
            _mockTransaction.Setup(tr => tr.Dispose()).Throws(new EntryNotFoundException(TestPlayerId));

            // Check that a TransactionAbortedException was thrown instead.
            var context = Util.CreateFakeCallContext(TestPlayerId, Pit);
            Assert.ThrowsAsync<TransactionAbortedException>(() =>
                _partyService.DeleteParty(new DeletePartyRequest(), context));
        }

        [Test]
        public void ReturnEmptyResponseWhenAPartyIsSuccessfullyDeleted()
        {
            // Setup the client so that it confirms that player is indeed a member of a party and that the deletion
            // request was successful.
            IEnumerable<Entry> deleted = null;
            _mockMemoryStoreClient.Setup(client => client.GetAsync<Member>(TestPlayerId))
                .ReturnsAsync(_testMember);
            _mockMemoryStoreClient.Setup(client => client.GetAsync<PartyDataModel>(_testMember.PartyId))
                .ReturnsAsync(_testParty);
            _mockTransaction.Setup(tr => tr.DeleteAll(It.IsAny<IEnumerable<Entry>>()))
                .Callback<IEnumerable<Entry>>(entries => deleted = entries);
            // We expect two different events -- one that the player was 'kicked' from the party, and one that
            // the party was deleted
            _mockAnalyticsSender.Setup(sender => sender.Send(AnalyticsConstants.PartyClass,
                                                             AnalyticsEventTypePartyDeletedMemberKicked,
                                                             new Dictionary<string, string> {
                                                                 { AnalyticsConstants.PlayerId, TestPlayerId },
                                                                 { AnalyticsConstants.PartyId, _testParty.Id }
                                                             }, TestPlayerId));
            _mockAnalyticsSender.Setup(sender => sender.Send(AnalyticsConstants.PartyClass,
                                                             AnalyticsEventTypePartyDeleted,
                                                             new Dictionary<string, string> {
                                                                 { AnalyticsConstants.PlayerId, TestPlayerId },
                                                                 { AnalyticsConstants.PartyId, _testParty.Id }
                                                             }, TestPlayerId));

            // Check that the deletion has completed without any errors raised and an empty response was returned as a
            // result.
            var context = Util.CreateFakeCallContext(TestPlayerId, Pit);
            var response = _partyService.DeleteParty(new DeletePartyRequest(), context).Result;
            Assert.AreEqual(new DeletePartyResponse(), response);

            var deletedList = deleted.ToList();
            Assert.AreEqual(2, deletedList.Count);

            var party = (PartyDataModel) deletedList[0];
            Assert.AreEqual(_testParty.Id, party.Id);

            var leader = (Member) deletedList[1];
            Assert.AreEqual(TestPlayerId, leader.Id);

            _mockAnalyticsSender.VerifyAll();
        }
    }
}
