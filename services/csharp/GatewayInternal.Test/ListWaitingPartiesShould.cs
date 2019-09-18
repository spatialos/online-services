using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Grpc.Core;
using Improbable.OnlineServices.DataModel;
using Improbable.OnlineServices.DataModel.Gateway;
using Improbable.OnlineServices.Proto.Gateway;
using MemoryStore;
using Moq;
using NUnit.Framework;
using PartyDataModel = Improbable.OnlineServices.DataModel.Party.Party;
using PartyProto = Improbable.OnlineServices.Proto.Party.Party;

namespace GatewayInternal.Test
{
    [TestFixture]
    public class PopWaitingPartiesShould
    {
        private const string MatchmakingType = "type_test";
        private const string SoloPartyLeader = "SoloPartyLeader";
        private const string TwoPlayerPartyLeader = "TwoPlayerPartyLeader";
        private const string TwoPlayerPartyMember = "TwoPlayerPartyMember";
        private const string Pit = "Pit";

        private static PartyDataModel _soloParty;
        private static PartyDataModel _twoPlayerParty;

        private Mock<ITransaction> _transaction;
        private Mock<IMemoryStoreClient> _memoryStoreClient;
        private GatewayInternalServiceImpl _service;

        [SetUp]
        public void Setup()
        {
            _soloParty = new PartyDataModel(SoloPartyLeader, Pit);
            _twoPlayerParty = new PartyDataModel(TwoPlayerPartyLeader, Pit);
            _twoPlayerParty.AddPlayerToParty(TwoPlayerPartyMember, Pit);

            _transaction = new Mock<ITransaction>(MockBehavior.Strict);
            _transaction.Setup(tx => tx.Dispose());

            _memoryStoreClient = new Mock<IMemoryStoreClient>(MockBehavior.Strict);
            _memoryStoreClient.Setup(client => client.Dispose());
            _memoryStoreClient.Setup(client => client.CreateTransaction()).Returns(_transaction.Object);

            var memoryStoreClientManager = new Mock<IMemoryStoreClientManager<IMemoryStoreClient>>();
            memoryStoreClientManager.Setup(manager => manager.GetClient()).Returns(_memoryStoreClient.Object);
            _service = new GatewayInternalServiceImpl(memoryStoreClientManager.Object);
        }

        [Test]
        public void ReturnInvalidArgumentWhenNumPartiesIsZero()
        {
            var context = Util.CreateFakeCallContext();
            var exception = Assert.ThrowsAsync<RpcException>(() => _service.PopWaitingParties(new PopWaitingPartiesRequest
            {
                Type = MatchmakingType,
                NumParties = 0
            }, context));
            Assert.AreEqual(StatusCode.InvalidArgument, exception.StatusCode);
        }

        [Test]
        public void ReturnResourceExhaustedWhenInsufficientPartiesAvailable()
        {
            _transaction.Setup(tx => tx.DequeueAsync(MatchmakingType, 2));
            _transaction.Setup(tx => tx.Dispose()).Throws<InsufficientEntriesException>();

            var context = Util.CreateFakeCallContext();
            var exception = Assert.ThrowsAsync<RpcException>(() => _service.PopWaitingParties(new PopWaitingPartiesRequest
            {
                Type = MatchmakingType,
                NumParties = 2
            }, context));
            Assert.AreEqual(StatusCode.ResourceExhausted, exception.StatusCode);
        }

        [Test]
        public void ReturnInternalIfPartyJoinRequestNotFound()
        {
            // Setup the client such that it will claim there is no JoinRequest associated with the dequeued party id.
            _transaction
                .Setup(tx => tx.DequeueAsync(MatchmakingType, 1))
                .Returns(Task.FromResult<IEnumerable<string>>(new List<string> { _soloParty.Id }));

            _memoryStoreClient.Setup(client => client.GetAsync<PartyJoinRequest>(_soloParty.Id))
                .ReturnsAsync((PartyJoinRequest) null);

            // Verify that an Internal StatusCode was returned.
            var context = Util.CreateFakeCallContext();
            var request = new PopWaitingPartiesRequest { Type = MatchmakingType, NumParties = 1 };
            var exception = Assert.ThrowsAsync<RpcException>(() => _service.PopWaitingParties(request, context));
            Assert.AreEqual(StatusCode.Internal, exception.StatusCode);
            Assert.AreEqual($"could not find JoinRequest for {_soloParty.Id}", exception.Status.Detail);
        }

        [Test]
        public void ReturnInternalIfPlayerJoinRequestNotFound()
        {
            // Setup the client such that it will claim there is no JoinRequest associated with one of the members of
            // the dequeued parties.
            _transaction
                .Setup(tx => tx.DequeueAsync(MatchmakingType, 1))
                .Returns(Task.FromResult<IEnumerable<string>>(new List<string> { _soloParty.Id }));

            _memoryStoreClient.Setup(client => client.GetAsync<PartyJoinRequest>(_soloParty.Id))
                .ReturnsAsync(new PartyJoinRequest(_soloParty, "", null));
            _memoryStoreClient.Setup(client => client.GetAsync<PlayerJoinRequest>(SoloPartyLeader))
                .ReturnsAsync((PlayerJoinRequest) null);

            // Verify that an Internal StatusCode was returned.
            var context = Util.CreateFakeCallContext();
            var request = new PopWaitingPartiesRequest { Type = MatchmakingType, NumParties = 1 };
            var exception = Assert.ThrowsAsync<RpcException>(() => _service.PopWaitingParties(request, context));
            Assert.AreEqual(StatusCode.Internal, exception.StatusCode);
            Assert.AreEqual($"could not find JoinRequest for {SoloPartyLeader}", exception.Status.Detail);
        }

        [Test]
        public void ReturnUnavailableErrorIfTransactionAborted()
        {
            _transaction.Setup(tx => tx.DequeueAsync(MatchmakingType, 1)).Throws<TransactionAbortedException>();
            var context = Util.CreateFakeCallContext();
            var exception = Assert.ThrowsAsync<RpcException>(() => _service.PopWaitingParties(new PopWaitingPartiesRequest
            {
                Type = MatchmakingType,
                NumParties = 1
            }, context));
            Assert.AreEqual(StatusCode.Unavailable, exception.StatusCode);
        }

        [Test]
        public void ReturnListOfPartiesWhenAvailable()
        {
            var metadata = new Dictionary<string, string> { { "region", "US" } };
            var updatedEntities = new List<PlayerJoinRequest>();
            _transaction
                .Setup(tx => tx.DequeueAsync(MatchmakingType, 2))
                .Returns(Task.FromResult<IEnumerable<string>>(new List<string> { _soloParty.Id, _twoPlayerParty.Id }));
            _memoryStoreClient
                .Setup(client => client.GetAsync<PartyJoinRequest>(_soloParty.Id))
                .ReturnsAsync(new PartyJoinRequest(_soloParty, MatchmakingType, metadata));
            _memoryStoreClient
                .Setup(client => client.GetAsync<PartyJoinRequest>(_twoPlayerParty.Id))
                .ReturnsAsync(new PartyJoinRequest(_twoPlayerParty, MatchmakingType, metadata));
            _memoryStoreClient
                .Setup(client => client.GetAsync<PlayerJoinRequest>(It.IsAny<string>()))
                .ReturnsAsync((string id) => new PlayerJoinRequest(id, Pit, MatchmakingType,"", "", metadata)
                { State = MatchState.Requested });
            _transaction
                .Setup(tx => tx.UpdateAll(It.IsAny<IEnumerable<Entry>>()))
                .Callback<IEnumerable<Entry>>(
                    playerRequests => updatedEntities.AddRange(playerRequests.Select(r => (PlayerJoinRequest) r)));

            var context = Util.CreateFakeCallContext();
            var resp = _service.PopWaitingParties(new PopWaitingPartiesRequest
            {
                Type = MatchmakingType,
                NumParties = 2
            }, context);
            Assert.That(resp.IsCompletedSuccessfully);
            Assert.AreEqual(StatusCode.OK, context.Status.StatusCode);

            var parties = resp.Result.Parties;
            Assert.AreEqual(2, parties.Count);
            Assert.AreEqual(_soloParty.Id, parties[0].Party.Id);
            Assert.AreEqual("US", parties[0].Metadata["region"]);
            Assert.AreEqual(_twoPlayerParty.Id, parties[1].Party.Id);
            Assert.AreEqual("US", parties[1].Metadata["region"]);

            // We are expecting 3 PlayerJoinRequests to be updated, one for each member of the two parties.
            Assert.AreEqual(3, updatedEntities.Count);
            Assert.AreEqual(SoloPartyLeader, updatedEntities[0].Id);
            Assert.AreEqual(MatchState.Matching, updatedEntities[0].State);
            Assert.AreEqual(TwoPlayerPartyLeader, updatedEntities[1].Id);
            Assert.AreEqual(MatchState.Matching, updatedEntities[1].State);
            Assert.AreEqual(TwoPlayerPartyMember, updatedEntities[2].Id);
            Assert.AreEqual(MatchState.Matching, updatedEntities[2].State);
        }
    }
}
