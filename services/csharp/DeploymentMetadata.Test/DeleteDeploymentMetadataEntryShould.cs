using Grpc.Core;
using Improbable.OnlineServices.Proto.Metadata;
using MemoryStore;
using Moq;
using NUnit.Framework;

namespace DeploymentMetadata.Test
{
    [TestFixture]
    public class DeleteDeploymentMetadataEntryShould
    {
        private const string SecretHeaderKey = "Secret";

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
        public void ReturnEmptyResponseWhenADeploymentMetadataEntryIsSuccessfullyDeleted()
        {
            string keyToDelete = null, hashFieldToDelete = null;
            _transaction.Setup(tx => tx.DeleteHashEntry(It.IsAny<string>(), It.IsAny<string>()))
                .Callback<string, string>((key, hashField) =>
                {
                    keyToDelete = key;
                    hashFieldToDelete = hashField;
                });

            var context = Util.CreateFakeCallContext(SecretHeaderKey);
            var request = new DeleteDeploymentMetadataEntryRequest
            {
                DeploymentId = DeploymentId,
                Key = "status"
            };

            var response = _service.DeleteDeploymentMetadataEntry(request, context).Result;

            Assert.AreEqual(new DeleteDeploymentMetadataEntryResponse(), response);
            Assert.AreEqual(DeploymentId, keyToDelete);
            Assert.AreEqual("status", hashFieldToDelete);
            Assert.AreEqual(StatusCode.OK, context.Status.StatusCode);
        }
    }
}
