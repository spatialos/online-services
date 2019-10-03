using System.Collections.Generic;
using System.Linq;
using Google.Api.Gax.Grpc;
using Grpc.Core;
using Improbable.OnlineServices.DataModel;
using Improbable.OnlineServices.DataModel.Gateway;
using Improbable.OnlineServices.Proto.Gateway;
using Improbable.SpatialOS.PlayerAuth.V2Alpha1;
using MemoryStore;
using Moq;
using NUnit.Framework;

namespace Gateway.Test
{
    [TestFixture]
    public class GatewayServiceGetJoinStatusShould
    {
        private const string Pit = "Pit";

        private GatewayServiceImpl _service;
        private Mock<IMemoryStoreClient> _memoryStoreClient;
        private Mock<ITransaction> _transaction;
        private Mock<PlayerAuthServiceClient> _authClient;

        [SetUp]
        public void Setup()
        {
            _memoryStoreClient = new Mock<IMemoryStoreClient>(MockBehavior.Strict);
            _transaction = new Mock<ITransaction>(MockBehavior.Strict);
            _transaction.Setup(tx => tx.Dispose());
            _authClient = new Mock<PlayerAuthServiceClient>(MockBehavior.Strict);
            var memoryStoreClientManager = new Mock<IMemoryStoreClientManager<IMemoryStoreClient>>();
            memoryStoreClientManager.Setup(manager => manager.GetClient()).Returns(_memoryStoreClient.Object);
            _memoryStoreClient.Setup(client => client.CreateTransaction()).Returns(_transaction.Object);
            _memoryStoreClient.Setup(client => client.Dispose());
            _service = new GatewayServiceImpl(memoryStoreClientManager.Object, _authClient.Object);
        }

        [Test]
        public void ReturnPermissionDeniedStatusIfRequestingOtherPlayersJoinStatus()
        {
            var context = Util.CreateFakeCallContext("wrong_id", Pit);
            var exception = Assert.ThrowsAsync<RpcException>(() =>
                _service.GetJoinStatus(new GetJoinStatusRequest { PlayerId = "test_op" }, context));
            Assert.AreEqual(StatusCode.PermissionDenied, exception.StatusCode);
        }

        [Test]
        public void ReturnNotFoundStatusIfJoinStatusDoesNotExist()
        {
            _memoryStoreClient.Setup(client => client.GetAsync<PlayerJoinRequest>("test_op"))
                .ReturnsAsync((PlayerJoinRequest) null);

            var context = Util.CreateFakeCallContext("test_op", Pit);
            var exception = Assert.ThrowsAsync<RpcException>(() =>
                _service.GetJoinStatus(new GetJoinStatusRequest { PlayerId = "test_op" }, context));
            Assert.AreEqual(StatusCode.NotFound, exception.StatusCode);
            Assert.That(exception.Message, Contains.Substring("requested player does not exist"));
        }

        [Test]
        public void ReturnUnavailableErrorIfTransactionAborted()
        {
            var joinReq = new PlayerJoinRequest("test_op", "", "", null);
            joinReq.State = MatchState.Matched;
            _memoryStoreClient.Setup(client => client.GetAsync<PlayerJoinRequest>("test_op")).ReturnsAsync(joinReq);
            _transaction.Setup(tx => tx.DeleteAll(It.IsAny<IEnumerable<Entry>>()))
                .Throws<TransactionAbortedException>();

            var context = Util.CreateFakeCallContext("test_op", Pit);
            var exception = Assert.ThrowsAsync<RpcException>(() =>
                _service.GetJoinStatus(new GetJoinStatusRequest { PlayerId = "test_op" }, context));
            Assert.AreEqual(StatusCode.Unavailable, exception.StatusCode);
            Assert.That(exception.Message, Contains.Substring("deletion aborted"));
        }

        [Test]
        public void ReturnJoinStatusWithResultIfMatched()
        {
            var joinReq = new PlayerJoinRequest("testplayer", "test-player-token", "open_world", null);
            joinReq.AssignMatch("1234", "deployment1234");
            _memoryStoreClient.Setup(client => client.GetAsync<PlayerJoinRequest>("test_op")).ReturnsAsync(joinReq);
            _authClient.Setup(client => client.CreateLoginTokenAsync(new CreateLoginTokenRequest
            {
                DeploymentId = "1234",
                PlayerIdentityToken = "test-player-token"
            }, It.IsAny<CallSettings>())).ReturnsAsync(new CreateLoginTokenResponse
            {
                LoginToken = "test-login-token"
            });
            var deleted = new List<PlayerJoinRequest>();
            _transaction.Setup(tx => tx.DeleteAll(It.IsAny<IEnumerable<Entry>>()))
                .Callback<IEnumerable<Entry>>(requests =>
                    deleted.AddRange(requests.Select(r => (PlayerJoinRequest) r)));

            var context = Util.CreateFakeCallContext("test_op", Pit);
            var resp = _service.GetJoinStatus(new GetJoinStatusRequest { PlayerId = "test_op" }, context);
            Assert.That(resp.IsCompletedSuccessfully);
            Assert.AreEqual(StatusCode.OK, context.Status.StatusCode);
            var op = resp.Result;
            Assert.That(op.Complete);
            Assert.AreEqual(GetJoinStatusResponse.Types.Status.Joined, op.Status);
            Assert.AreEqual("deployment1234", op.DeploymentName);
            Assert.AreEqual("test-login-token", op.LoginToken);
            Assert.AreEqual("testplayer", deleted[0].PlayerIdentity);
            Assert.AreEqual("open_world", deleted[0].Type);
        }

        [Test]
        public void ReturnJoinStatusWithNotDoneIfStateMatching()
        {
            var joinReq = new PlayerJoinRequest("test_op", "", "", null);
            joinReq.State = MatchState.Matching;
            _memoryStoreClient.Setup(client => client.GetAsync<PlayerJoinRequest>("test_op")).ReturnsAsync(joinReq);
            var context = Util.CreateFakeCallContext("test_op", Pit);
            var resp = _service.GetJoinStatus(new GetJoinStatusRequest { PlayerId = "test_op" }, context);
            Assert.That(resp.IsCompletedSuccessfully);
            Assert.AreEqual(StatusCode.OK, context.Status.StatusCode);
            var op = resp.Result;
            Assert.That(!op.Complete);
            Assert.AreEqual(GetJoinStatusResponse.Types.Status.Matching, op.Status);
            _transaction.Verify(tx => tx.DeleteAll(It.IsAny<IEnumerable<Entry>>()), Times.Never);
        }

        [Test]
        public void ReturnJoinStatusWithNotDoneIfStateRequested()
        {
            var joinReq = new PlayerJoinRequest("test_op", "", "", null);
            _memoryStoreClient.Setup(client => client.GetAsync<PlayerJoinRequest>("test_op")).ReturnsAsync(joinReq);
            var context = Util.CreateFakeCallContext("test_op", Pit);
            var resp = _service.GetJoinStatus(new GetJoinStatusRequest { PlayerId = "test_op" }, context);
            Assert.That(resp.IsCompletedSuccessfully);
            Assert.AreEqual(StatusCode.OK, context.Status.StatusCode);
            var op = resp.Result;
            Assert.That(!op.Complete);
            Assert.AreEqual(GetJoinStatusResponse.Types.Status.Waiting, op.Status);
            _transaction.Verify(tx => tx.DeleteAll(It.IsAny<IEnumerable<Entry>>()), Times.Never);
        }

        [Test]
        public void ReturnUnknownErrorIfJoinRequestErrored()
        {
            var joinReq = new PlayerJoinRequest("test_op", "", "", null);
            joinReq.State = MatchState.Error;
            _memoryStoreClient.Setup(client => client.GetAsync<PlayerJoinRequest>("test_op")).ReturnsAsync(joinReq);
            var deleted = new List<PlayerJoinRequest>();
            _transaction.Setup(tx => tx.DeleteAll(It.IsAny<IEnumerable<Entry>>()))
                .Callback<IEnumerable<Entry>>(requests =>
                    deleted.AddRange(requests.Select(r => (PlayerJoinRequest) r)));

            var context = Util.CreateFakeCallContext("test_op", Pit);
            var resp = _service.GetJoinStatus(new GetJoinStatusRequest { PlayerId = "test_op" }, context);
            Assert.That(resp.IsCompletedSuccessfully);
            Assert.AreEqual(StatusCode.OK, context.Status.StatusCode);
            var op = resp.Result;
            Assert.That(op.Complete);
            Assert.That(op.Error, Contains.Substring("join request encountered an error"));
            Assert.AreEqual("test_op", deleted[0].PlayerIdentity);
        }
    }
}
