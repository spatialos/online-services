using System.Collections.Generic;
using Grpc.Core;
using Improbable.OnlineServices.Proto.Metadata;
using MemoryStore;
using Moq;
using NUnit.Framework;
using DeploymentMetadataModel = Improbable.OnlineServices.DataModel.Metadata.DeploymentMetadata;

namespace DeploymentMetadata.Test
{
    [TestFixture]
    public class DeleteDeploymentMetadataShould
    {
        private const string SecretHeaderKey = "Secret";

        private const string DeploymentId = "1234567890";

        private static readonly Dictionary<string, string> _testMetadata = new Dictionary<string, string>
            {{"status", "Ready"}};

        private static readonly DeploymentMetadataModel _deploymentMetadataModel =
            new DeploymentMetadataModel(DeploymentId, _testMetadata);

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
        public void ReturnNotImplementedError()
        {
            var context = Util.CreateFakeCallContext(SecretHeaderKey);
            var request = new DeleteDeploymentMetadataRequest
            {
                DeploymentId = DeploymentId
            };

            var exception = Assert.ThrowsAsync<RpcException>(() =>
                _service.DeleteDeploymentMetadata(request, context));

            Assert.AreEqual(StatusCode.Unimplemented, exception.StatusCode);
        }
    }
}
