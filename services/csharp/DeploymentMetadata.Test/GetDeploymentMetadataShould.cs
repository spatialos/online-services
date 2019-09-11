using System.Collections.Generic;
using Grpc.Core;
using Improbable.OnlineServices.Proto.Metadata;
using MemoryStore;
using Moq;
using NUnit.Framework;

namespace DeploymentMetadata.Test
{
    [TestFixture]
    public class GetDeploymentMetadataShould
    {
        private const string DeploymentId = "1234567890";

        private static readonly Dictionary<string, string> TestMetadata = new Dictionary<string, string>
            {{"status", "Ready"}};

        private Mock<ITransaction> _transaction;
        private Mock<IMemoryStoreClient> _mockMemoryStoreClient;
        private DeploymentMetadataImpl _service;

        [SetUp]
        public void SetUp()
        {
            _transaction = new Mock<ITransaction>(MockBehavior.Strict);
            _transaction.Setup(tx => tx.Dispose());

            _mockMemoryStoreClient = new Mock<IMemoryStoreClient>(MockBehavior.Strict);
            _mockMemoryStoreClient.Setup(client => client.Dispose());
            _mockMemoryStoreClient.Setup(client => client.CreateTransaction()).Returns(_transaction.Object);

            var memoryStoreClientManager = new Mock<IMemoryStoreClientManager<IMemoryStoreClient>>();
            memoryStoreClientManager.Setup(manager => manager.GetClient()).Returns(_mockMemoryStoreClient.Object);
            _service = new DeploymentMetadataImpl(memoryStoreClientManager.Object);
        }


        [Test]
        public void ThrowNotFoundExceptionWhenKeyDoesNotExist()
        {
            _mockMemoryStoreClient.Setup(client => client.GetHashAsync(DeploymentId)).ReturnsAsync((Dictionary<string, string>) null);

            var context = Util.CreateFakeCallContext(Util.Auth.Authenticated);
            var request = new GetDeploymentMetadataRequest
            {
                DeploymentId = DeploymentId
            };

            var exception = Assert.ThrowsAsync<RpcException>(() => _service.GetDeploymentMetadata(request, context));

            Assert.AreEqual(StatusCode.NotFound, exception.StatusCode);
        }

        [Test]
        public void ReturnDeploymentMetadataWhenKeyExists()
        {
            _mockMemoryStoreClient.Setup(client => client.GetHashAsync(DeploymentId)).ReturnsAsync(TestMetadata);

            var context = Util.CreateFakeCallContext(Util.Auth.Authenticated);
            var request = new GetDeploymentMetadataRequest
            {
                DeploymentId = DeploymentId
            };

            var metadata = _service.GetDeploymentMetadata(request, context);

            Assert.IsTrue(metadata.IsCompleted);
            CollectionAssert.AreEqual(metadata.Result.Value, TestMetadata);
        }
    }
}
