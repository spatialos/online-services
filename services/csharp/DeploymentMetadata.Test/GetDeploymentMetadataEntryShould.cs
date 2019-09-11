using Grpc.Core;
using Improbable.OnlineServices.Proto.Metadata;
using MemoryStore;
using Moq;
using NUnit.Framework;

namespace DeploymentMetadata.Test
{
    [TestFixture]
    public class GetDeploymentMetadataEntryShould
    {
        private const string DeploymentId = "1234567890";

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
        public void ThrowNotFoundExceptionWhenEntryDoesNotExist()
        {
            const string metadataKey = "status";

            _mockMemoryStoreClient.Setup(client => client.GetHashEntryAsync(DeploymentId, metadataKey))
                .ReturnsAsync((string) null);

            var context = Util.CreateFakeCallContext(Util.Auth.Authenticated);
            var request = new GetDeploymentMetadataEntryRequest
            {
                DeploymentId = DeploymentId,
                Key = metadataKey
            };

            var exception = Assert.ThrowsAsync<RpcException>(
                () => _service.GetDeploymentMetadataEntry(request, context)
            );

            Assert.AreEqual(StatusCode.NotFound, exception.StatusCode);
        }

        [Test]
        public void ReturnDeploymentMetadataEntryWhenEntryExists()
        {
            const string metadataKey = "status";
            const string metadataValue = "Ready";

            _mockMemoryStoreClient.Setup(client => client.GetHashEntryAsync(DeploymentId, metadataKey))
                .ReturnsAsync(metadataValue);

            var context = Util.CreateFakeCallContext(Util.Auth.Authenticated);
            var request = new GetDeploymentMetadataEntryRequest
            {
                DeploymentId = DeploymentId,
                Key = metadataKey
            };

            var metadata = _service.GetDeploymentMetadataEntry(request, context);

            Assert.IsTrue(metadata.IsCompleted);
            CollectionAssert.AreEqual(metadata.Result.Value, metadataValue);
        }
    }
}
