using System.Collections.Generic;
using Grpc.Core;
using Improbable.OnlineServices.Proto.Metadata;
using MemoryStore;
using Moq;
using NUnit.Framework;

namespace DeploymentMetadata.Test
{
    [TestFixture]
    public class SetDeploymentMetadataEntryShould
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
        public void ThrowInvalidArgumentErrorWhenDeploymentIdEmpty()
        {
            var context = Util.CreateFakeCallContext(SecretHeaderKey);
            var request = new SetDeploymentMetadataEntryRequest
            {
                DeploymentId = "",
                Key = "status",
                Value = "Not Ready"
            };

            var exception = Assert.ThrowsAsync<RpcException>(() =>
                _service.SetDeploymentMetadataEntry(request, context));

            Assert.AreEqual(StatusCode.InvalidArgument, exception.StatusCode);
            Assert.That(exception.Message, Contains.Substring("DeploymentId"));
        }

        [Test]
        public void ThrowInvalidArgumentErrorWhenHashKeyEmpty()
        {
            var context = Util.CreateFakeCallContext(SecretHeaderKey);
            var request = new SetDeploymentMetadataEntryRequest
            {
                DeploymentId = DeploymentId,
                Key = "",
                Value = "Not Ready"
            };

            var exception = Assert.ThrowsAsync<RpcException>(() =>
                _service.SetDeploymentMetadataEntry(request, context));

            Assert.AreEqual(StatusCode.InvalidArgument, exception.StatusCode);
            Assert.That(exception.Message, Contains.Substring("Key"));
        }

        [Test]
        public void SetNoConditionsWhenNoConditionProvidedInRequest()
        {
            var context = Util.CreateFakeCallContext(SecretHeaderKey);
            var request = new SetDeploymentMetadataEntryRequest
            {
                DeploymentId = DeploymentId,
                Key = "status",
                Value = "Not Ready",
                Condition = new Condition
                {
                    Function = Condition.Types.Function.NoCondition
                }
            };

            _transaction.Setup(tx => tx.UpdateHashWithEntries(DeploymentId, new Dictionary<string, string>
            {
                {"status", "Not Ready"}
            }));

            _service.SetDeploymentMetadataEntry(request, context);

            // Two invocations - the UpdateHashWithEntries call and the Dispose.
            // No conditions.
            Assert.AreEqual(2, _transaction.Invocations.Count);
        }

        [Test]
        public void SetCorrectConditionForExistsCheck()
        {
            var context = Util.CreateFakeCallContext(SecretHeaderKey);
            var request = new SetDeploymentMetadataEntryRequest
            {
                DeploymentId = DeploymentId,
                Key = "status",
                Value = "Not Ready",
                Condition = new Condition
                {
                    Function = Condition.Types.Function.Exists
                }
            };

            _transaction.Setup(tx => tx.UpdateHashWithEntries(DeploymentId, new Dictionary<string, string>
            {
                {"status", "Not Ready"}
            }));

            _transaction.Setup(tx => tx.AddHashEntryExistsCondition(DeploymentId, "status"));

            _service.SetDeploymentMetadataEntry(request, context);

            // Three invocations - the UpdateHashWithEntries call, Dispose and Condition.
            Assert.AreEqual(3, _transaction.Invocations.Count);
        }

        [Test]
        public void SetCorrectConditionForNotExistsCheck()
        {
            var context = Util.CreateFakeCallContext(SecretHeaderKey);
            var request = new SetDeploymentMetadataEntryRequest
            {
                DeploymentId = DeploymentId,
                Key = "status",
                Value = "Not Ready",
                Condition = new Condition
                {
                    Function = Condition.Types.Function.NotExists
                }
            };

            _transaction.Setup(tx => tx.UpdateHashWithEntries(DeploymentId, new Dictionary<string, string>
            {
                {"status", "Not Ready"}
            }));

            _transaction.Setup(tx => tx.AddHashEntryNotExistsCondition(DeploymentId, "status"));

            _service.SetDeploymentMetadataEntry(request, context);

            // Three invocations - the UpdateHashWithEntries call, Dispose and Condition.
            Assert.AreEqual(3, _transaction.Invocations.Count);
        }

        [Test]
        public void SetCorrectConditionForEqualCheck()
        {
            var context = Util.CreateFakeCallContext(SecretHeaderKey);
            var request = new SetDeploymentMetadataEntryRequest
            {
                DeploymentId = DeploymentId,
                Key = "status",
                Value = "Not Ready",
                Condition = new Condition
                {
                    Function = Condition.Types.Function.Equal,
                    Payload = "In Use"
                }
            };

            _transaction.Setup(tx => tx.UpdateHashWithEntries(DeploymentId, new Dictionary<string, string>
            {
                {"status", "Not Ready"}
            }));

            _transaction.Setup(tx => tx.AddHashEntryEqualCondition(DeploymentId, "status", "In Use"));

            _service.SetDeploymentMetadataEntry(request, context);

            // Three invocations - the UpdateHashWithEntries call, Dispose and Condition.
            Assert.AreEqual(3, _transaction.Invocations.Count);
        }

        [Test]
        public void SetCorrectConditionForNotEqualCheck()
        {
            var context = Util.CreateFakeCallContext(SecretHeaderKey);
            var request = new SetDeploymentMetadataEntryRequest
            {
                DeploymentId = DeploymentId,
                Key = "status",
                Value = "Not Ready",
                Condition = new Condition
                {
                    Function = Condition.Types.Function.NotEqual,
                    Payload = "In Use"
                }
            };

            _transaction.Setup(tx => tx.UpdateHashWithEntries(DeploymentId, new Dictionary<string, string>
            {
                {"status", "Not Ready"}
            }));

            _transaction.Setup(tx => tx.AddHashEntryNotEqualCondition(DeploymentId, "status", "In Use"));

            _service.SetDeploymentMetadataEntry(request, context);

            // Three invocations - the UpdateHashWithEntries call, Dispose and Condition.
            Assert.AreEqual(3, _transaction.Invocations.Count);
        }
    }
}
