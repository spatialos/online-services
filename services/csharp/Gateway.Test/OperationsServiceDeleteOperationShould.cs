using System.Collections.Generic;
using System.Linq;
using Google.LongRunning;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Improbable.OnlineServices.DataModel;
using Improbable.OnlineServices.DataModel.Gateway;
using Improbable.OnlineServices.DataModel.Party;
using MemoryStore;
using Moq;
using NUnit.Framework;

namespace Gateway.Test
{
    [TestFixture]
    public class OperationsServiceDeleteOperationShould
    {
        private const string LeaderId = "LeaderId";
        private const string PlayerId = "PlayerId";
        private const string Pit = "Pit";
        private static Party _party;

        private Mock<ITransaction> _transaction;
        private Mock<IMemoryStoreClient> _memoryStoreClient;
        private OperationsServiceImpl _service;

        [SetUp]
        public void Setup()
        {
            _party = new Party(LeaderId, Pit);
            _party.AddPlayerToParty(PlayerId, Pit);

            _transaction = new Mock<ITransaction>(MockBehavior.Strict);
            _transaction.Setup(tx => tx.Dispose());
            _memoryStoreClient = new Mock<IMemoryStoreClient>(MockBehavior.Strict);

            _memoryStoreClient.Setup(client => client.CreateTransaction()).Returns(_transaction.Object);
            _memoryStoreClient.Setup(client => client.Dispose());

            var memoryStoreClientManager = new Mock<IMemoryStoreClientManager<IMemoryStoreClient>>();
            memoryStoreClientManager.Setup(manager => manager.GetClient()).Returns(_memoryStoreClient.Object);
            _service = new OperationsServiceImpl(memoryStoreClientManager.Object, null);
        }

        [Test]
        public void ReturnPermissionDeniedStatusIfDeletingOtherPlayersOperation()
        {
            var context = Util.CreateFakeCallContext(PlayerId, Pit);
            var exception = Assert.ThrowsAsync<RpcException>(() =>
                _service.DeleteOperation(new DeleteOperationRequest { Name = LeaderId }, context));
            Assert.AreEqual(StatusCode.PermissionDenied, exception.StatusCode);
        }

        [Test]
        public void ReturnOkCodeIfDeletionSuccessful()
        {
            // Setup the client such that it will successfully cancel matchmaking.
            _memoryStoreClient.Setup(client => client.GetAsync<Member>(LeaderId)).ReturnsAsync(_party.GetLeader);
            _memoryStoreClient.Setup(client => client.GetAsync<Party>(_party.Id)).ReturnsAsync(_party);

            _memoryStoreClient.Setup(client => client.GetAsync<PartyJoinRequest>(_party.Id))
                .ReturnsAsync(new PartyJoinRequest(_party, "", null));
            _memoryStoreClient.Setup(client => client.GetAsync<PlayerJoinRequest>(LeaderId))
                .ReturnsAsync(new PlayerJoinRequest(LeaderId, "", "", "", "", null));
            _memoryStoreClient.Setup(client => client.GetAsync<PlayerJoinRequest>(PlayerId))
                .ReturnsAsync(new PlayerJoinRequest(PlayerId, "", "", "", "", null));

            var deleted = new List<Entry>();
            _transaction.Setup(tx => tx.DeleteAll(It.IsAny<IEnumerable<Entry>>()))
                .Callback<IEnumerable<Entry>>(entities => deleted.AddRange(entities));

            var updated = new List<Entry>();
            _transaction.Setup(tx => tx.UpdateAll(It.IsAny<IEnumerable<Entry>>()))
                .Callback<IEnumerable<Entry>>(entities => updated.AddRange(entities));

            var dequeued = new List<PartyJoinRequest>();
            _transaction.Setup(tx => tx.RemoveAllFromQueue(It.IsAny<IEnumerable<QueuedEntry>>()))
                .Callback<IEnumerable<QueuedEntry>>(requests =>
                    dequeued.AddRange(requests.Select(r => (PartyJoinRequest) r)));

            var context = Util.CreateFakeCallContext(LeaderId, Pit);
            var response = _service.DeleteOperation(new DeleteOperationRequest { Name = LeaderId }, context);
            Assert.That(response.IsCompleted);
            Assert.IsInstanceOf<Empty>(response.Result);
            Assert.AreEqual(StatusCode.OK, context.Status.StatusCode);

            // We expect the Party to return to the Forming phase
            Assert.AreEqual(1, updated.Count);
            Assert.AreEqual(Party.Phase.Forming, ((Party) updated[0]).CurrentPhase);

            // We expect one PartyJoinRequest to have been dequeued, as the leader has cancelled matchmaking.
            Assert.AreEqual(_party.Id, dequeued[0].Id);

            // We expect the PartyJoinRequest associated to the party to have been deleted as well.
            Assert.AreEqual(_party.Id, deleted[0].Id);

            Assert.AreEqual(3, deleted.Count);
            Assert.AreEqual(LeaderId, deleted[1].Id);
            Assert.AreEqual(PlayerId, deleted[2].Id);
        }

        [Test]
        public void ReturnNotFoundStatusIfPartyNotInMatchmaking()
        {
            _memoryStoreClient.Setup(client => client.GetAsync<Member>(LeaderId)).ReturnsAsync(_party.GetLeader);
            _memoryStoreClient.Setup(client => client.GetAsync<Party>(_party.Id)).ReturnsAsync(_party);

            _memoryStoreClient.Setup(client => client.GetAsync<PartyJoinRequest>(_party.Id))
                .ReturnsAsync(new PartyJoinRequest(_party, "", null));
            _memoryStoreClient.Setup(client => client.GetAsync<PlayerJoinRequest>(LeaderId))
                .ReturnsAsync(new PlayerJoinRequest(LeaderId, "", "", "", "", null));
            _memoryStoreClient.Setup(client => client.GetAsync<PlayerJoinRequest>(PlayerId))
                .ReturnsAsync(new PlayerJoinRequest(PlayerId, "", "", "", "", null));

            _transaction.Setup(tr => tr.RemoveAllFromQueue(It.IsAny<IEnumerable<QueuedEntry>>()));
            _transaction.Setup(tr => tr.Dispose()).Throws(new EntryNotFoundException(_party.Id));

            var context = Util.CreateFakeCallContext(LeaderId, Pit);
            var exception = Assert.ThrowsAsync<RpcException>(() =>
                _service.DeleteOperation(new DeleteOperationRequest { Name = LeaderId }, context));
            Assert.AreEqual(StatusCode.NotFound, exception.StatusCode);
            Assert.That(exception.Message, Contains.Substring("party is not in matchmaking"));
        }

        [Test]
        public void ReturnNotFoundStatusIfJoinRequestNotFoundForPartyMember()
        {
            _memoryStoreClient.Setup(client => client.GetAsync<Member>(LeaderId)).ReturnsAsync(_party.GetLeader);
            _memoryStoreClient.Setup(client => client.GetAsync<Party>(_party.Id)).ReturnsAsync(_party);

            _memoryStoreClient.Setup(client => client.GetAsync<PartyJoinRequest>(_party.Id))
                .ReturnsAsync(new PartyJoinRequest(_party, "", null));
            _memoryStoreClient.Setup(client => client.GetAsync<PlayerJoinRequest>(LeaderId))
                .ReturnsAsync(new PlayerJoinRequest(LeaderId, "", "", "", "", null));
            _memoryStoreClient.Setup(client => client.GetAsync<PlayerJoinRequest>(PlayerId))
                .ReturnsAsync(new PlayerJoinRequest(PlayerId, "", "", "", "", null));

            _transaction.Setup(tr => tr.UpdateAll(It.IsAny<IEnumerable<Entry>>()));
            _transaction.Setup(tr => tr.RemoveAllFromQueue(It.IsAny<IEnumerable<QueuedEntry>>()));
            _transaction.Setup(tr => tr.Dispose()).Throws(new EntryNotFoundException(PlayerId));

            var context = Util.CreateFakeCallContext(LeaderId, Pit);
            var exception = Assert.ThrowsAsync<RpcException>(() =>
                _service.DeleteOperation(new DeleteOperationRequest { Name = LeaderId }, context));
            Assert.AreEqual(StatusCode.Internal, exception.StatusCode);
        }

        [Test]
        public void ReturnsUnavailableErrorIfTransactionAborted()
        {
            _memoryStoreClient.Setup(client => client.GetAsync<Member>(LeaderId)).ReturnsAsync(_party.GetLeader);
            _memoryStoreClient.Setup(client => client.GetAsync<Party>(_party.Id)).ReturnsAsync(_party);

            _memoryStoreClient.Setup(client => client.GetAsync<PartyJoinRequest>(_party.Id))
                .ReturnsAsync(new PartyJoinRequest(_party, "", null));
            _memoryStoreClient.Setup(client => client.GetAsync<PlayerJoinRequest>(LeaderId))
                .ReturnsAsync(new PlayerJoinRequest(LeaderId, "", "", "", "", null));
            _memoryStoreClient.Setup(client => client.GetAsync<PlayerJoinRequest>(PlayerId))
                .ReturnsAsync(new PlayerJoinRequest(PlayerId, "", "", "", "", null));

            _transaction.Setup(tr => tr.UpdateAll(It.IsAny<IEnumerable<Entry>>()));
            _transaction.Setup(tr => tr.RemoveAllFromQueue(It.IsAny<IEnumerable<QueuedEntry>>()));
            _transaction.Setup(tr => tr.Dispose()).Throws<TransactionAbortedException>();

            var context = Util.CreateFakeCallContext(LeaderId, Pit);
            var exception = Assert.ThrowsAsync<RpcException>(() =>
                _service.DeleteOperation(new DeleteOperationRequest { Name = LeaderId }, context));
            Assert.AreEqual(StatusCode.Unavailable, exception.StatusCode);
        }

        [Test]
        public void ReturnPermissionDeniedIfThePlayerIsNotTheLeader()
        {
            _memoryStoreClient.Setup(client => client.GetAsync<Member>(PlayerId)).ReturnsAsync(_party.GetMember(PlayerId));
            _memoryStoreClient.Setup(client => client.GetAsync<Party>(_party.Id)).ReturnsAsync(_party);

            var context = Util.CreateFakeCallContext(PlayerId, Pit);
            var exception = Assert.ThrowsAsync<RpcException>(() =>
                _service.DeleteOperation(new DeleteOperationRequest { Name = PlayerId }, context));
            Assert.AreEqual(StatusCode.PermissionDenied, exception.StatusCode);
        }

        [Test]
        public void ReturnNotFoundIfThePlayerIsNotAMemberOfAnyParty()
        {
            _memoryStoreClient.Setup(client => client.GetAsync<Member>(PlayerId)).ReturnsAsync((Member) null);

            var context = Util.CreateFakeCallContext(PlayerId, Pit);
            var exception = Assert.ThrowsAsync<RpcException>(() =>
                _service.DeleteOperation(new DeleteOperationRequest { Name = PlayerId }, context));
            Assert.AreEqual(StatusCode.NotFound, exception.StatusCode);
            Assert.That(exception.Message, Contains.Substring("not a member of any party"));
        }
    }
}
