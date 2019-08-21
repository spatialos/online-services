using Google.LongRunning;
using Grpc.Core;
using Improbable.SpatialOS.PlayerAuth.V2Alpha1;
using MemoryStore;
using Moq;
using NUnit.Framework;

namespace Gateway.Test
{
    [TestFixture]
    public class OperationsServiceCancelOperationShould
    {
        private OperationsServiceImpl _service;

        [SetUp]
        public void Setup()
        {
            var memoryStoreClient = new Mock<IMemoryStoreClient>();
            var authClient = new Mock<PlayerAuthServiceClient>();
            var memoryStoreClientManager = new Mock<IMemoryStoreClientManager<IMemoryStoreClient>>();
            memoryStoreClientManager.Setup(manager => manager.GetClient()).Returns(memoryStoreClient.Object);
            _service = new OperationsServiceImpl(memoryStoreClientManager.Object, authClient.Object);
        }

        [Test]
        public void ThrowUnimplementedException()
        {
            var exception = Assert.Throws<RpcException>(() =>
                _service.CancelOperation(new CancelOperationRequest { Name = "test_id" }, null));
            Assert.AreEqual(StatusCode.Unimplemented, exception.Status.StatusCode);
        }
    }
}
