using System.Collections.Generic;
using System.Linq;
using Grpc.Core;
using Improbable.OnlineServices.DataModel;
using Improbable.OnlineServices.DataModel.Gateway;
using Improbable.OnlineServices.DataModel.Party;
using MemoryStore;
using Moq;
using NUnit.Framework;
using JoinRequestProto = Improbable.OnlineServices.Proto.Gateway.JoinRequest;

namespace Gateway.Test
{
    [TestFixture]
    public class GatewayServiceJoinShould
    {
        private const string LeaderId = "LeaderId";
        private const string PlayerId = "PlayerId";
        private const string MatchmakingType = "open_world";
        private const string Pit = "Pit";

        private static Party _party;

        private Mock<ITransaction> _transaction;
        private Mock<IMemoryStoreClient> _memClient;
        private GatewayServiceImpl _service;

        [SetUp]
        public void Setup()
        {
            _party = new Party(LeaderId, Pit);
            _party.AddPlayerToParty(PlayerId, Pit);

            _transaction = new Mock<ITransaction>(MockBehavior.Strict);
            _transaction.Setup(tx => tx.Dispose());

            _memClient = new Mock<IMemoryStoreClient>(MockBehavior.Strict);
            _memClient.Setup(client => client.CreateTransaction()).Returns(_transaction.Object);
            _memClient.Setup(client => client.Dispose());

            var memoryStoreClientManager = new Mock<IMemoryStoreClientManager<IMemoryStoreClient>>();
            memoryStoreClientManager.Setup(manager => manager.GetClient()).Returns(_memClient.Object);
            _service = new GatewayServiceImpl(memoryStoreClientManager.Object, null);
        }

        [Test]
        public void ReturnNotFoundIfNoSuchPartyExists()
        {
            _memClient.Setup(client => client.GetAsync<Member>(LeaderId)).ReturnsAsync((Member) null);

            var context = Util.CreateFakeCallContext(LeaderId, Pit);
            var req = new JoinRequestProto
            {
                MatchmakingType = MatchmakingType,
            };
            var exception = Assert.ThrowsAsync<RpcException>(() => _service.Join(req, context));
            Assert.AreEqual(StatusCode.NotFound, exception.StatusCode);
        }

        [Test]
        public void ReturnPermissionDeniedIfThePlayerMakingTheRequestIsNotTheLeader()
        {
            _memClient.Setup(client => client.GetAsync<Member>(PlayerId)).ReturnsAsync(_party.GetMember(PlayerId));
            _memClient.Setup(client => client.GetAsync<Party>(_party.Id)).ReturnsAsync(_party);

            var context = Util.CreateFakeCallContext(PlayerId, Pit);
            var req = new JoinRequestProto
            {
                MatchmakingType = MatchmakingType,
            };
            var exception = Assert.ThrowsAsync<RpcException>(() => _service.Join(req, context));
            Assert.AreEqual(StatusCode.PermissionDenied, exception.StatusCode);
            Assert.That(exception.Message, Contains.Substring("Only the leader"));
        }

        [Test]
        public void ReturnFailedPreconditionIfThereAreNotEnoughMembers()
        {
            _party.UpdateMinMaxMembers(10, 20);
            _memClient.Setup(client => client.GetAsync<Member>(LeaderId)).ReturnsAsync(_party.GetLeader);
            _memClient.Setup(client => client.GetAsync<Party>(_party.Id)).ReturnsAsync(_party);

            var context = Util.CreateFakeCallContext(LeaderId, Pit);
            var req = new JoinRequestProto
            {
                MatchmakingType = MatchmakingType,
            };
            var exception = Assert.ThrowsAsync<RpcException>(() => _service.Join(req, context));
            Assert.AreEqual(StatusCode.FailedPrecondition, exception.StatusCode);
            Assert.That(exception.Message, Contains.Substring("not enough members"));
        }

        [Test]
        public void ReturnOkIfSuccessful()
        {
            // Setup the client such that it will successfully queue the party.
            _memClient.Setup(client => client.GetAsync<Member>(LeaderId)).ReturnsAsync(_party.GetLeader);
            _memClient.Setup(client => client.GetAsync<Party>(_party.Id)).ReturnsAsync(_party);

            var created = new List<Entry>();
            _transaction.Setup(tx => tx.CreateAll(It.IsAny<IEnumerable<Entry>>()))
                .Callback<IEnumerable<Entry>>(entities => created.AddRange(entities));

            var updated = new List<Party>();
            _transaction.Setup(tx => tx.UpdateAll(It.IsAny<IEnumerable<Entry>>()))
                .Callback<IEnumerable<Entry>>(parties => updated.AddRange(parties.Select(party => (Party) party)));

            var queued = new List<PartyJoinRequest>();
            _transaction.Setup(tx => tx.EnqueueAll(It.IsAny<IEnumerable<QueuedEntry>>()))
                .Callback<IEnumerable<Entry>>(requests => queued.AddRange(requests.Select(r => (PartyJoinRequest) r)));

            var context = Util.CreateFakeCallContext(LeaderId, Pit);
            var req = new JoinRequestProto
            {
                MatchmakingType = MatchmakingType,
            };
            req.Metadata.Add("region", "eu");

            var resp = _service.Join(req, context);
            Assert.That(resp.IsCompletedSuccessfully);
            Assert.AreEqual(StatusCode.OK, context.Status.StatusCode);

            // Three entities should have been created: a PartyJoinRequest (which is also put in the queue) and two
            // PlayerJoinRequests, one per each member of the party.
            Assert.AreEqual(3, created.Count);
            Assert.IsInstanceOf<PartyJoinRequest>(created[0]);
            Assert.IsInstanceOf<PlayerJoinRequest>(created[1]);
            Assert.IsInstanceOf<PlayerJoinRequest>(created[2]);

            var leaderJoinRequest = (PlayerJoinRequest) created[1];
            Assert.AreEqual(LeaderId, leaderJoinRequest.PlayerIdentity);
            Assert.AreEqual(MatchmakingType, leaderJoinRequest.Type);
            Assert.AreEqual(MatchState.Requested, leaderJoinRequest.State);
            Assert.AreEqual("eu", leaderJoinRequest.Metadata["region"]);

            var playerJoinRequest = (PlayerJoinRequest) created[2];
            Assert.AreEqual(PlayerId, playerJoinRequest.PlayerIdentity);
            Assert.AreEqual(MatchmakingType, playerJoinRequest.Type);
            Assert.AreEqual(MatchState.Requested, playerJoinRequest.State);
            Assert.AreEqual("eu", playerJoinRequest.Metadata["region"]);

            // Verify that only one PartyJoinRequest has been enqueued.
            Assert.AreEqual(1, queued.Count);
            Assert.AreEqual(_party.Id, queued[0].Party.Id);
            Assert.AreEqual(MatchmakingType, queued[0].Type);
            Assert.AreEqual("eu", queued[0].Metadata["region"]);
            Assert.AreEqual(MatchmakingType, queued[0].QueueName);

            // Verify that the party's CurrentPhase has been updated to matchmaking.
            Assert.AreEqual(1, updated.Count);
            Assert.AreEqual(_party.Id, updated[0].Id);
            Assert.AreEqual(Party.Phase.Matchmaking, updated[0].CurrentPhase);
        }

        [Test]
        public void ReturnNotFoundIfPartyWasDeletedMeanwhile()
        {
            // Setup the client such that it will claim the party was deleted meanwhile.
            _memClient.Setup(client => client.GetAsync<Member>(LeaderId)).ReturnsAsync(_party.GetLeader);
            _memClient.Setup(client => client.GetAsync<Party>(_party.Id)).ReturnsAsync(_party);
            _transaction.Setup(tx => tx.CreateAll(It.IsAny<IEnumerable<Entry>>()));
            _transaction.Setup(tx => tx.UpdateAll(It.IsAny<IEnumerable<Entry>>()));
            _transaction.Setup(tx => tx.EnqueueAll(It.IsAny<IEnumerable<QueuedEntry>>()));
            _transaction.Setup(tx => tx.Dispose()).Throws(new EntryNotFoundException(_party.Id));

            var context = Util.CreateFakeCallContext(LeaderId, Pit);
            var req = new JoinRequestProto
            {
                MatchmakingType = MatchmakingType,
            };
            req.Metadata.Add("region", "eu");

            var exception = Assert.ThrowsAsync<RpcException>(() => _service.Join(req, context));
            Assert.AreEqual(StatusCode.NotFound, exception.StatusCode);
            Assert.That(exception.Message, Contains.Substring("not a member of any party"));
        }

        [Test]
        public void ReturnAlreadyExistsIfPartyIsAlreadyQueued()
        {
            // Setup the client such that it will claim the party is already in the queue.
            _memClient.Setup(client => client.GetAsync<Member>(LeaderId)).ReturnsAsync(_party.GetLeader);
            _memClient.Setup(client => client.GetAsync<Party>(_party.Id)).ReturnsAsync(_party);
            _transaction.Setup(tx => tx.CreateAll(It.IsAny<IEnumerable<Entry>>()));
            _transaction.Setup(tx => tx.UpdateAll(It.IsAny<IEnumerable<Entry>>()));
            _transaction.Setup(tx => tx.EnqueueAll(It.IsAny<IEnumerable<QueuedEntry>>()));
            _transaction.Setup(tx => tx.Dispose()).Throws(new EntryAlreadyExistsException(_party.Id));

            var context = Util.CreateFakeCallContext(LeaderId, Pit);
            var req = new JoinRequestProto
            {
                MatchmakingType = MatchmakingType,
            };
            req.Metadata.Add("region", "eu");

            var exception = Assert.ThrowsAsync<RpcException>(() => _service.Join(req, context));
            Assert.AreEqual(StatusCode.AlreadyExists, exception.StatusCode);
        }

        [Test]
        public void ReturnUnavailableIfTransactionAborted()
        {
            // Setup the client such that it will claim that there were concurrency issues encountered while committing
            // the transaction.
            _memClient.Setup(client => client.GetAsync<Member>(LeaderId)).ReturnsAsync(_party.GetLeader);
            _memClient.Setup(client => client.GetAsync<Party>(_party.Id)).ReturnsAsync(_party);
            _transaction.Setup(tx => tx.CreateAll(It.IsAny<IEnumerable<Entry>>()));
            _transaction.Setup(tx => tx.UpdateAll(It.IsAny<IEnumerable<Entry>>()));
            _transaction.Setup(tx => tx.EnqueueAll(It.IsAny<IEnumerable<QueuedEntry>>()));
            _transaction.Setup(tx => tx.Dispose()).Throws<TransactionAbortedException>();

            var context = Util.CreateFakeCallContext(LeaderId, Pit);
            var req = new JoinRequestProto
            {
                MatchmakingType = MatchmakingType,
            };
            var exception = Assert.ThrowsAsync<RpcException>(() => _service.Join(req, context));
            Assert.AreEqual(StatusCode.Unavailable, exception.StatusCode);
        }
    }
}
