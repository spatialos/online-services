using System.Collections.Generic;
using Grpc.Core;
using Improbable.OnlineServices.DataModel.Party;
using Improbable.OnlineServices.Proto.Party;
using MemoryStore;
using Moq;
using NUnit.Framework;
using PartyDataModel = Improbable.OnlineServices.DataModel.Party.Party;

namespace Party.Test
{
    [TestFixture]
    public class GetPartyByPlayerIdShould
    {
        private const string TestPlayerId = "Gridelwald2018";
        private const string Pit = "PIT";

        private static readonly Dictionary<string, string> _testMetadata = new Dictionary<string, string>
            {{"location", "Paris"}};

        private static readonly PartyDataModel _party =
            new PartyDataModel(TestPlayerId, Pit, 2, 5, _testMetadata);

        private Mock<IMemoryStoreClient> _mockMemoryStoreClient;
        private PartyServiceImpl _partyService;

        [SetUp]
        public void SetUp()
        {
            _mockMemoryStoreClient = new Mock<IMemoryStoreClient>(MockBehavior.Strict);
            _mockMemoryStoreClient.Setup(client => client.Dispose()).Verifiable();
            var memoryStoreClientManager = new Mock<IMemoryStoreClientManager<IMemoryStoreClient>>(MockBehavior.Strict);
            memoryStoreClientManager.Setup(manager => manager.GetClient()).Returns(_mockMemoryStoreClient.Object);
            _partyService = new PartyServiceImpl(memoryStoreClientManager.Object);
        }

        [Test]
        public void ReturnNotFoundWhenThePlayerIsNotAMemberOfAnyParty()
        {
            // Setup the client such that it will confirm that TestPlayer is not a member of any party.
            _mockMemoryStoreClient.Setup(client => client.Get<Member>(TestPlayerId)).Returns((Member) null);

            // Check that a GrpcException as thrown as a result.
            var context = Util.CreateFakeCallContext(TestPlayerId, Pit);
            var exception = Assert.Throws<RpcException>(() =>
                _partyService.GetPartyByPlayerId(new GetPartyByPlayerIdRequest(), context));
            Assert.That(exception.Message, Contains.Substring("not a member of any"));
            Assert.AreEqual(StatusCode.NotFound, exception.StatusCode);
        }

        [Test]
        public void ReturnNotFoundWhenThePartyNoLongerExists()
        {
            // Setup the client such that it will claim the party has been deleted between the time of Get<Member> and
            // Get<PartyDataModel>.
            _mockMemoryStoreClient.Setup(client => client.Get<Member>(TestPlayerId)).Returns(_party.GetLeader());
            _mockMemoryStoreClient.Setup(client => client.Get<PartyDataModel>(_party.Id))
                .Returns((PartyDataModel) null);

            // Check that a GrpcException as thrown as a result.
            var context = Util.CreateFakeCallContext(TestPlayerId, Pit);
            var exception = Assert.Throws<RpcException>(() =>
                _partyService.GetPartyByPlayerId(new GetPartyByPlayerIdRequest(), context));
            Assert.That(exception.Message, Contains.Substring("not a member of any"));
            Assert.AreEqual(StatusCode.NotFound, exception.StatusCode);
        }

        [Test]
        public void ReturnPartyWhenPlayerIsAMemberOfIt()
        {
            // Setup the client such that it returns a party associated to TestPlayer.
            _mockMemoryStoreClient.Setup(client => client.Get<Member>(TestPlayerId)).Returns(_party.GetLeader());
            _mockMemoryStoreClient.Setup(client => client.Get<PartyDataModel>(_party.Id)).Returns(_party);

            // Make sure that the party has been successfully returned, having the expected fields.
            var context = Util.CreateFakeCallContext(TestPlayerId, Pit);
            var getPartyResponse = _partyService.GetPartyByPlayerId(new GetPartyByPlayerIdRequest(), context).Result;

            var receivedParty = getPartyResponse.Party;
            Assert.AreEqual(_party.Id, receivedParty.Id);
            Assert.AreEqual(TestPlayerId, receivedParty.LeaderPlayerId);
            Assert.AreEqual(_party.MinMembers, receivedParty.MinMembers);
            Assert.AreEqual(_party.MaxMembers, receivedParty.MaxMembers);
            Assert.AreEqual(_party.CurrentPhase.ToString(), receivedParty.CurrentPhase.ToString());
            CollectionAssert.AreEquivalent(_testMetadata, receivedParty.Metadata);
            CollectionAssert.AreEquivalent(_party.MemberIdToPit, receivedParty.MemberIdToPit);
        }
    }
}