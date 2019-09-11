using Grpc.Core;
using Improbable.OnlineServices.Proto.Metadata;
using MemoryStore;
using Moq;
using NUnit.Framework;

namespace DeploymentMetadata.Test
{
    [TestFixture]
    public class DeleteDeploymentMetadataShould
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
        public void ReturnEmptyResponseWhenADeploymentMetadataIsSuccessfullyDeleted()
        {
            string keyToDelete = null;
            _transaction.Setup(tx => tx.DeleteKey(It.IsAny<string>())).Callback<string>(key => keyToDelete = key);

            var context = Util.CreateFakeCallContext(Util.Auth.Authenticated);
            var request = new DeleteDeploymentMetadataRequest
            {
                DeploymentId = DeploymentId
            };

            var response = _service.DeleteDeploymentMetadata(request, context).Result;

            Assert.AreEqual(new DeleteDeploymentMetadataResponse(), response);
            Assert.AreEqual(DeploymentId, keyToDelete);
            Assert.AreEqual(StatusCode.OK, context.Status.StatusCode);
        }
    }
}
