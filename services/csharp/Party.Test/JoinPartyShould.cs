using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
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

namespace Party.Test
{
    [TestFixture]
    public class JoinPartyShould
    {
        private const string TestLeaderId = "Leader";
        private const string TestPlayerId = "Gridelwald2018";
        private const string Pit = "PIT";
        private const string AnalyticsEventType = "player_joined_party";

        private static PartyDataModel _partyToJoin;
        private static PlayerInvites _playerInvites;
        private static Invite _invite;

        private Mock<ITransaction> _mockTransaction;
        private Mock<IMemoryStoreClient> _mockMemoryStoreClient;
        private Mock<IAnalyticsSender> _mockAnalyticsSender;
        private PartyServiceImpl _partyService;

        [SetUp]
        public void SetUp()
        {
            _partyToJoin = new PartyDataModel(TestLeaderId, Pit);
            _playerInvites = new PlayerInvites(TestPlayerId, ImmutableHashSet.Create("invite"), ImmutableHashSet<string>.Empty);
            _invite = new Invite(TestLeaderId, TestPlayerId, _partyToJoin.Id);

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
        public void ReturnInvalidArgumentWhenNoPartyIdIsProvided()
        {
            // Check that having a non-empty party id is enforced by JoinParty.
            var context = Util.CreateFakeCallContext(TestPlayerId, Pit);
            var exception = Assert.ThrowsAsync<RpcException>(() => _partyService.JoinParty(new JoinPartyRequest(), context));
            Assert.That(exception.Message, Contains.Substring("requires a non-empty party"));
            Assert.AreEqual(StatusCode.InvalidArgument, exception.StatusCode);
        }

        [Test]
        public void ReturnNotFoundWhenTheGivenPartyDoesNotExist()
        {
            // Setup the client such that it will claim that there aren't any parties having as id TestPartyId.
            _mockMemoryStoreClient.Setup(client => client.GetAsync<PartyDataModel>(_partyToJoin.Id))
                .ReturnsAsync((PartyDataModel) null);

            // Check that an exception was thrown when trying to join a non-existing party.
            var context = Util.CreateFakeCallContext(TestPlayerId, Pit);
            var request = new JoinPartyRequest { PartyId = _partyToJoin.Id };
            var exception = Assert.ThrowsAsync<RpcException>(() => _partyService.JoinParty(request, context));
            Assert.That(exception.Message, Contains.Substring("party doesn't exist"));
            Assert.AreEqual(StatusCode.NotFound, exception.StatusCode);
        }

        [Test]
        public void ReturnAlreadyExistsWhenThePlayerIsAMemberOfAnotherParty()
        {
            // Setup the client such that it will claim that the player is already a member of another party. 
            _mockMemoryStoreClient.Setup(client => client.GetAsync<PartyDataModel>(_partyToJoin.Id)).ReturnsAsync(_partyToJoin);
            _mockMemoryStoreClient.Setup(client => client.GetAsync<Member>(TestPlayerId))
                .ReturnsAsync(new Member(TestPlayerId, "AnotherPartyId"));

            // Check that an exception was thrown when trying to join a party.
            var context = Util.CreateFakeCallContext(TestPlayerId, Pit);
            var request = new JoinPartyRequest { PartyId = _partyToJoin.Id };
            var exception = Assert.ThrowsAsync<RpcException>(() => _partyService.JoinParty(request, context));
            Assert.That(exception.Message, Contains.Substring("player is a member of another party"));
            Assert.AreEqual(StatusCode.AlreadyExists, exception.StatusCode);
        }


        [Test]
        public void ReturnFailedPreconditionWhenThePlayerDoesNotHaveAnyInvites()
        {
            // Setup the client such that it will claim that the party is not in the Forming phase.
            _partyToJoin.CurrentPhase = PartyDataModel.Phase.Matchmaking;
            _mockMemoryStoreClient.Setup(client => client.GetAsync<PartyDataModel>(_partyToJoin.Id)).ReturnsAsync(_partyToJoin);
            _mockMemoryStoreClient.Setup(client => client.GetAsync<Member>(TestPlayerId)).ReturnsAsync((Member) null);
            _mockMemoryStoreClient.Setup(client => client.GetAsync<PlayerInvites>(TestPlayerId)).ReturnsAsync((PlayerInvites) null);

            // Check that an exception was thrown when trying to rejoin the party.
            var context = Util.CreateFakeCallContext(TestPlayerId, Pit);
            var request = new JoinPartyRequest { PartyId = _partyToJoin.Id };
            var exception = Assert.ThrowsAsync<RpcException>(() => _partyService.JoinParty(request, context));
            Assert.That(exception.Message, Contains.Substring("The player is not invited to this party"));
            Assert.AreEqual(StatusCode.FailedPrecondition, exception.StatusCode);
        }

        [Test]
        public void ReturnFailedPreconditionWhenThePlayerDoesNotHaveAValidInvite()
        {
            var inviteB =
            // Setup the client such that it will claim that the party is not in the Forming phase.
            _partyToJoin.CurrentPhase = PartyDataModel.Phase.Matchmaking;
            _mockMemoryStoreClient.Setup(client => client.GetAsync<PartyDataModel>(_partyToJoin.Id)).ReturnsAsync(_partyToJoin);
            _mockMemoryStoreClient.Setup(client => client.GetAsync<Member>(TestPlayerId)).ReturnsAsync((Member) null);
            _mockMemoryStoreClient.Setup(client => client.GetAsync<PlayerInvites>(TestPlayerId)).ReturnsAsync(_playerInvites);
            _mockMemoryStoreClient.Setup(client => client.GetAsync<Invite>("invite")).ReturnsAsync((Invite) null);

            // Check that an exception was thrown when trying to rejoin the party.
            var context = Util.CreateFakeCallContext(TestPlayerId, Pit);
            var request = new JoinPartyRequest { PartyId = _partyToJoin.Id };
            var exception = Assert.ThrowsAsync<RpcException>(() => _partyService.JoinParty(request, context));
            Assert.That(exception.Message, Contains.Substring("The player is not invited to this party"));
            Assert.AreEqual(StatusCode.FailedPrecondition, exception.StatusCode);
        }

        [Test]
        public void ReturnFailedPreconditionWhenThePartyIsNotInFormingPhase()
        {
            // Setup the client such that it will claim that the party is not in the Forming phase.
            _partyToJoin.CurrentPhase = PartyDataModel.Phase.Matchmaking;
            _mockMemoryStoreClient.Setup(client => client.GetAsync<PartyDataModel>(_partyToJoin.Id)).ReturnsAsync(_partyToJoin);
            _mockMemoryStoreClient.Setup(client => client.GetAsync<Member>(TestPlayerId)).ReturnsAsync((Member) null);
            _mockMemoryStoreClient.Setup(client => client.GetAsync<PlayerInvites>(TestPlayerId)).ReturnsAsync(_playerInvites);
            _mockMemoryStoreClient.Setup(client => client.GetAsync<Invite>("invite")).ReturnsAsync(_invite);

            // Check that an exception was thrown when trying to rejoin the party.
            var context = Util.CreateFakeCallContext(TestPlayerId, Pit);
            var request = new JoinPartyRequest { PartyId = _partyToJoin.Id };
            var exception = Assert.ThrowsAsync<RpcException>(() => _partyService.JoinParty(request, context));
            Assert.That(exception.Message, Contains.Substring("party is no longer in the Forming phase"));
            Assert.AreEqual(StatusCode.FailedPrecondition, exception.StatusCode);
        }

        [Test]
        public void ReturnFailedPreconditionIfPartyIsAtFullCapacity()
        {
            // Setup the client such that it will claim that the party is at full capacity.
            _partyToJoin.UpdateMinMaxMembers(1, 1);
            _mockMemoryStoreClient.Setup(client => client.GetAsync<PartyDataModel>(_partyToJoin.Id)).ReturnsAsync(_partyToJoin);
            _mockMemoryStoreClient.Setup(client => client.GetAsync<Member>(TestPlayerId)).ReturnsAsync((Member) null);
            _mockMemoryStoreClient.Setup(client => client.GetAsync<PlayerInvites>(TestPlayerId)).ReturnsAsync(_playerInvites);
            _mockMemoryStoreClient.Setup(client => client.GetAsync<Invite>("invite")).ReturnsAsync(_invite);

            // Check that an exception was thrown when trying to join the party.
            var context = Util.CreateFakeCallContext(TestPlayerId, Pit);
            var request = new JoinPartyRequest { PartyId = _partyToJoin.Id };
            var exception = Assert.ThrowsAsync<RpcException>(() => _partyService.JoinParty(request, context));
            Assert.That(exception.Message, Contains.Substring("full capacity"));
            Assert.AreEqual(StatusCode.FailedPrecondition, exception.StatusCode);
        }

        [Test]
        public void ReturnEarlyWhenThePlayerIsAlreadyAMemberOfTheParty()
        {
            // Setup the client such that it will claim that the player is already a member of the party they're trying
            // to join.
            _partyToJoin.AddPlayerToParty(TestPlayerId, Pit);
            _mockMemoryStoreClient.Setup(client => client.GetAsync<PartyDataModel>(_partyToJoin.Id)).ReturnsAsync(_partyToJoin);
            _mockMemoryStoreClient.Setup(client => client.GetAsync<Member>(TestPlayerId))
                .ReturnsAsync(_partyToJoin.GetMember(TestPlayerId));
            _mockMemoryStoreClient.Setup(client => client.GetAsync<PlayerInvites>(TestPlayerId)).ReturnsAsync(_playerInvites);
            _mockMemoryStoreClient.Setup(client => client.GetAsync<Invite>("invite")).ReturnsAsync(_invite);

            var context = Util.CreateFakeCallContext(TestPlayerId, Pit);
            var request = new JoinPartyRequest { PartyId = _partyToJoin.Id };
            var response = _partyService.JoinParty(request, context).Result;
            Assert.NotNull(response.Party);
        }

        [Test]
        public void ReturnPartyWhenSuccessfullyJoiningTheParty()
        {
            // Setup the client such that it will successfully add the player to the party. 
            var entriesCreated = new List<Entry>();
            var entriesUpdated = new List<Entry>();
            _mockMemoryStoreClient.Setup(client => client.GetAsync<PartyDataModel>(_partyToJoin.Id)).ReturnsAsync(_partyToJoin);
            _mockMemoryStoreClient.Setup(client => client.GetAsync<Member>(TestPlayerId)).ReturnsAsync((Member) null);
            _mockTransaction.Setup(tr => tr.CreateAll(It.IsAny<IEnumerable<Entry>>()))
                .Callback<IEnumerable<Entry>>(entries => entriesCreated.AddRange(entries));
            _mockTransaction.Setup(tr => tr.UpdateAll(It.IsAny<IEnumerable<Entry>>()))
                .Callback<IEnumerable<Entry>>(entries => entriesUpdated.AddRange(entries));
            _mockTransaction.Setup(tr => tr.Dispose());
            _mockMemoryStoreClient.Setup(client => client.GetAsync<PlayerInvites>(TestPlayerId)).ReturnsAsync(_playerInvites);
            _mockMemoryStoreClient.Setup(client => client.GetAsync<Invite>("invite")).ReturnsAsync(_invite);
            _mockAnalyticsSender.Setup(
                sender => sender.Send(AnalyticsConstants.PartyClass, AnalyticsEventType,
                                      It.Is<Dictionary<string, object>>(d =>
                                                                            AnalyticsAttributesExpectations(d)), TestPlayerId));


            // Check that the join was successfully completed and that the expected party was returned.
            var context = Util.CreateFakeCallContext(TestPlayerId, Pit);
            var request = new JoinPartyRequest { PartyId = _partyToJoin.Id };
            var party = _partyService.JoinParty(request, context).Result.Party;
            Assert.IsNotNull(party);
            Assert.AreEqual(_partyToJoin.Id, party.Id);
            Assert.Contains(TestPlayerId, party.MemberIds);

            // Verify that the expected entries were created/updated.
            Assert.AreEqual(1, entriesCreated.Count);
            Assert.IsInstanceOf<Member>(entriesCreated[0]);

            var member = (Member) entriesCreated[0];
            Assert.AreEqual(TestPlayerId, member.Id);
            Assert.AreEqual(_partyToJoin.Id, member.PartyId);

            Assert.AreEqual(1, entriesUpdated.Count);
            Assert.IsInstanceOf<PartyDataModel>(entriesUpdated[0]);
            var updatedParty = (PartyDataModel) entriesUpdated[0];
            Assert.AreEqual(_partyToJoin.Id, updatedParty.Id);
            Assert.IsNotNull(updatedParty.GetMember(TestPlayerId));

            _mockAnalyticsSender.VerifyAll();
        }

        private bool AnalyticsAttributesExpectations(Dictionary<string, object> dictionary)
        {
            return dictionary[AnalyticsConstants.PlayerId] is string playerId
                && playerId == TestPlayerId
                && dictionary[AnalyticsConstants.PartyId] is string partyId &&
                   partyId == _partyToJoin.Id
                && dictionary[AnalyticsConstants.Invites] is IEnumerable<Dictionary<string, string>> invites &&
                   invites.ToList() is List<Dictionary<string, string>> inviteList &&
                   inviteList.Count == 1 &&
                   inviteList[0][AnalyticsConstants.InviteId] == _invite.Id &&
                   inviteList[0][AnalyticsConstants.PlayerIdInviter] == _invite.SenderId;
        }
    }
}
