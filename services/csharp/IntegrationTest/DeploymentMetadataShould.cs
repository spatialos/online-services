using System;
using Grpc.Core;
using Improbable.OnlineServices.Proto.Metadata;
using MemoryStore.Redis;
using NUnit.Framework;

namespace IntegrationTest
{
    public class DeploymentMetadataShould
    {
        private const string RedisConnection = "localhost:6379";
        private const string MetadataTarget = "localhost:4043";

        private string _metadataServerSecret;
        private DeploymentMetadataService.DeploymentMetadataServiceClient _metadataClient;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _metadataServerSecret = Environment.GetEnvironmentVariable("DEPLOYMENT_METADATA_SERVER_SECRET");
            if (string.IsNullOrEmpty(_metadataServerSecret))
            {
                Assert.Fail("Metadata server secret is missing from environment.");
            }

            _metadataClient = new DeploymentMetadataService.DeploymentMetadataServiceClient(
                new Channel(MetadataTarget, ChannelCredentials.Insecure)
            );
        }

        [TearDown]
        public void TearDown()
        {
            using (var memoryStoreManager = new RedisClientManager(RedisConnection))
            {
                var client = memoryStoreManager.GetRawClient(Database.Metadata);
                client.Execute("flushdb");
            }
        }

        [Test]
        public void ReturnPermissionDeniedErrorIfSecretNotProvided()
        {
            var exception = Assert.Throws<RpcException>(
                () => _metadataClient.SetDeploymentMetadataEntry(
                    new SetDeploymentMetadataEntryRequest
                    {
                        DeploymentId = "my-deployment",
                        Key = "my-key",
                        Value = "my-value"
                    }
                )
            );

            Assert.AreEqual(StatusCode.PermissionDenied, exception.StatusCode);
        }
    }
}
