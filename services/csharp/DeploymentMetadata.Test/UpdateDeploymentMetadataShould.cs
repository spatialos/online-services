using System.Collections.Generic;
using Grpc.Core;
using Improbable.OnlineServices.Proto.Metadata;
using MemoryStore;
using Moq;
using NUnit.Framework;

namespace DeploymentMetadata.Test
{
    [TestFixture]
    public class UpdateDeploymentMetadataShould
    {
        private const string SecretHeaderKey = "Secret";

        private const string DeploymentId = "1234567890";

        private Mock<ITransaction> _transaction;
        private DeploymentMetadataImpl _service;

        [SetUp]
        public void SetUp()
        {
            _transaction = new Mock<ITransaction>(MockBehavior.Strict);
            _transaction.Setup(tx => tx.Dispose());

            var mockMemoryStoreClient = new Mock<IMemoryStoreClient>(MockBehavior.Strict);
            mockMemoryStoreClient.Setup(client => client.Dispose());
            mockMemoryStoreClient.Setup(client => client.CreateTransaction()).Returns(_transaction.Object);

            var memoryStoreClientManager = new Mock<IMemoryStoreClientManager<IMemoryStoreClient>>();
            memoryStoreClientManager.Setup(manager => manager.GetClient()).Returns(mockMemoryStoreClient.Object);
            _service = new DeploymentMetadataImpl(memoryStoreClientManager.Object);
        }

        [Test]
        public void CallsCorrectMemoryStoreMethod()
        {
            var context = Util.CreateFakeCallContext(SecretHeaderKey);

            _transaction
                .Setup(tx => tx.UpdateHashWithEntries(DeploymentId, new Dictionary<string, string>
                {
                    {"status", "Not Ready"}
                }));

            var request = new UpdateDeploymentMetadataRequest
            {
                DeploymentId = DeploymentId,
                Metadata =
                {
                    {"status", "Not Ready"}
                }
            };

            var response = _service.UpdateDeploymentMetadata(request, context);
            Assert.That(response.IsCompletedSuccessfully);
        }
    }
}
