using System;
using System.Collections.Generic;
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
        public void AllowForDeploymentMetadataAnnotationAndRetrieval()
        {
            var condition = new Condition
            {
                Function = Condition.Types.Function.NoCondition
            };

            var authHeaders = GetAuthenticationHeader();

            var setDeploymentMetadataRequest = new SetDeploymentMetadataEntryRequest
            {
                Condition = condition,
                DeploymentId = "1234",
                Key = "random key",
                Value = "random value"
            };
            _metadataClient.SetDeploymentMetadataEntry(setDeploymentMetadataRequest, authHeaders);

            var getDeploymentMetadataRequest = new GetDeploymentMetadataEntryRequest
            {
                DeploymentId = "1234",
                Key = "random key"
            };
            var response = _metadataClient.GetDeploymentMetadataEntry(getDeploymentMetadataRequest, authHeaders);

            Assert.AreEqual(response.Value, "random value");
        }

        [Test]
        public void AllowForDeploymentMetadataAnnotationAndNoConditionUpdate()
        {
            var authHeaders = GetAuthenticationHeader();

            var updateDeploymentMetadataRequest = new UpdateDeploymentMetadataRequest
            {
                DeploymentId = "1234",
                Metadata =
                {
                    {"malleable", "shall be changed"}
                }
            };
            _metadataClient.UpdateDeploymentMetadata(updateDeploymentMetadataRequest, authHeaders);

            var condition = new Condition
            {
                Function = Condition.Types.Function.NoCondition
            };

            var setDeploymentMetadataRequest = new SetDeploymentMetadataEntryRequest
            {
                Condition = condition,
                DeploymentId = "1234",
                Key = "malleable",
                Value = "none"
            };
            _metadataClient.SetDeploymentMetadataEntry(setDeploymentMetadataRequest, authHeaders);

            var getDeploymentMetadataRequest = new GetDeploymentMetadataEntryRequest
            {
                DeploymentId = "1234",
                Key = "malleable"
            };
            var response = _metadataClient.GetDeploymentMetadataEntry(getDeploymentMetadataRequest, authHeaders);

            Assert.AreEqual(response.Value, "none");
        }

        [Test]
        public void AllowForDeploymentMetadataAnnotationAndEqualConditionUpdate()
        {
            var authHeaders = GetAuthenticationHeader();

            var updateDeploymentMetadataRequest = new UpdateDeploymentMetadataRequest()
            {
                DeploymentId = "1234",
                Metadata =
                {
                    {"malleable", "shall be changed"}
                }
            };
            _metadataClient.UpdateDeploymentMetadata(updateDeploymentMetadataRequest, authHeaders);

            var wrongCondition = new Condition
            {
                Function = Condition.Types.Function.Equal,
                Payload = "none"
            };

            var correctCondition = new Condition
            {
                Function = Condition.Types.Function.Equal,
                Payload = "shall be changed"
            };

            var setWrongDeploymentMetadataRequest = new SetDeploymentMetadataEntryRequest
            {
                Condition = wrongCondition,
                DeploymentId = "1234",
                Key = "malleable",
                Value = "success"
            };

            var exception = Assert.Throws<RpcException>(() =>
            {
                _metadataClient.SetDeploymentMetadataEntry(setWrongDeploymentMetadataRequest, authHeaders);
            });

            Assert.AreEqual(exception.StatusCode, StatusCode.FailedPrecondition);

            var setCorrectDeploymentMetadataRequest = new SetDeploymentMetadataEntryRequest
            {
                Condition = correctCondition,
                DeploymentId = "1234",
                Key = "malleable",
                Value = "success"
            };
            _metadataClient.SetDeploymentMetadataEntry(setCorrectDeploymentMetadataRequest, authHeaders);

            var getDeploymentMetadataRequest = new GetDeploymentMetadataEntryRequest
            {
                DeploymentId = "1234",
                Key = "malleable"
            };
            var response = _metadataClient.GetDeploymentMetadataEntry(getDeploymentMetadataRequest, authHeaders);

            Assert.AreEqual(response.Value, "success");
        }

        [Test]
        public void AllowForDeploymentMetadataAnnotationAndNotEqualConditionUpdate()
        {
            var authHeaders = GetAuthenticationHeader();

            var updateDeploymentMetadataRequest = new UpdateDeploymentMetadataRequest()
            {
                DeploymentId = "1234",
                Metadata =
                {
                    {"malleable", "shall be changed"}
                }
            };
            _metadataClient.UpdateDeploymentMetadata(updateDeploymentMetadataRequest, authHeaders);

            var wrongCondition = new Condition
            {
                Function = Condition.Types.Function.NotEqual,
                Payload = "shall be changed"
            };

            var correctCondition = new Condition
            {
                Function = Condition.Types.Function.NotEqual,
                Payload = "is being changed"
            };

            var setWrongDeploymentMetadataRequest = new SetDeploymentMetadataEntryRequest
            {
                Condition = wrongCondition,
                DeploymentId = "1234",
                Key = "malleable",
                Value = "success"
            };

            var exception = Assert.Throws<RpcException>(() =>
            {
                _metadataClient.SetDeploymentMetadataEntry(setWrongDeploymentMetadataRequest, authHeaders);
            });

            Assert.AreEqual(exception.StatusCode, StatusCode.FailedPrecondition);

            var setCorrectDeploymentMetadataRequest = new SetDeploymentMetadataEntryRequest
            {
                Condition = correctCondition,
                DeploymentId = "1234",
                Key = "malleable",
                Value = "success"
            };
            _metadataClient.SetDeploymentMetadataEntry(setCorrectDeploymentMetadataRequest, authHeaders);

            var getDeploymentMetadataRequest = new GetDeploymentMetadataEntryRequest
            {
                DeploymentId = "1234",
                Key = "malleable"
            };
            var response = _metadataClient.GetDeploymentMetadataEntry(getDeploymentMetadataRequest, authHeaders);

            Assert.AreEqual(response.Value, "success");
        }

        [Test]
        public void AllowForDeploymentMetadataAnnotationAndExistsConditionUpdate()
        {
            var authHeaders = GetAuthenticationHeader();

            var updateDeploymentMetadataRequest = new UpdateDeploymentMetadataRequest()
            {
                DeploymentId = "1234",
                Metadata =
                {
                    {"findable", "shall be changed"},
                }
            };
            _metadataClient.UpdateDeploymentMetadata(updateDeploymentMetadataRequest, authHeaders);

            var condition = new Condition
            {
                Function = Condition.Types.Function.Exists
            };

            var setWrongDeploymentMetadataRequest = new SetDeploymentMetadataEntryRequest
            {
                Condition = condition,
                DeploymentId = "1234",
                Key = "missing",
                Value = "missing"
            };

            var exception = Assert.Throws<RpcException>(() =>
            {
                _metadataClient.SetDeploymentMetadataEntry(setWrongDeploymentMetadataRequest, authHeaders);
            });

            Assert.AreEqual(exception.StatusCode, StatusCode.NotFound);

            var setCorrectDeploymentMetadataRequest = new SetDeploymentMetadataEntryRequest
            {
                Condition = condition,
                DeploymentId = "1234",
                Key = "findable",
                Value = "success"
            };
            _metadataClient.SetDeploymentMetadataEntry(setCorrectDeploymentMetadataRequest, authHeaders);

            var getDeploymentMetadataRequest = new GetDeploymentMetadataEntryRequest
            {
                DeploymentId = "1234",
                Key = "findable"
            };
            var response = _metadataClient.GetDeploymentMetadataEntry(getDeploymentMetadataRequest, authHeaders);

            Assert.AreEqual(response.Value, "success");
        }

        [Test]
        public void AllowForDeploymentMetadataAnnotationAndNotExistsConditionUpdate()
        {
            var authHeaders = GetAuthenticationHeader();

            var updateDeploymentMetadataRequest = new UpdateDeploymentMetadataRequest()
            {
                DeploymentId = "1234",
                Metadata =
                {
                    {"existing", "just exists"}
                }
            };
            _metadataClient.UpdateDeploymentMetadata(updateDeploymentMetadataRequest, authHeaders);

            var condition = new Condition
            {
                Function = Condition.Types.Function.NotExists
            };

            var setWrongDeploymentMetadataRequest = new SetDeploymentMetadataEntryRequest
            {
                Condition = condition,
                DeploymentId = "1234",
                Key = "existing",
                Value = "can't be changed"
            };

            var exception = Assert.Throws<RpcException>(() =>
            {
                _metadataClient.SetDeploymentMetadataEntry(setWrongDeploymentMetadataRequest, authHeaders);
            });

            Assert.AreEqual(exception.StatusCode, StatusCode.AlreadyExists);

            var setCorrectDeploymentMetadataRequest = new SetDeploymentMetadataEntryRequest
            {
                Condition = condition,
                DeploymentId = "1234",
                Key = "new",
                Value = "entry"
            };
            _metadataClient.SetDeploymentMetadataEntry(setCorrectDeploymentMetadataRequest, authHeaders);

            var getDeploymentMetadataRequest = new GetDeploymentMetadataRequest
            {
                DeploymentId = "1234"
            };
            var response = _metadataClient.GetDeploymentMetadata(getDeploymentMetadataRequest, authHeaders);

            Assert.AreEqual(response.Value, new Dictionary<string, string>
            {
                {"existing", "just exists"},
                {"new", "entry"}
            });
        }

        [Test]
        public void AllowForDeploymentMetadataAnnotationAndDeletion()
        {
            var authHeaders = GetAuthenticationHeader();

            var updateDeploymentMetadataRequest = new UpdateDeploymentMetadataRequest()
            {
                DeploymentId = "1234",
                Metadata =
                {
                    {"random key 1", "random value 1"},
                    {"random key 2", "random value 2"}
                }
            };
            _metadataClient.UpdateDeploymentMetadata(updateDeploymentMetadataRequest, authHeaders);

            var deleteDeploymentMetadataRequest = new DeleteDeploymentMetadataRequest()
            {
                DeploymentId = "1234"
            };

            _metadataClient.DeleteDeploymentMetadata(deleteDeploymentMetadataRequest, authHeaders);

            var response = Assert.Throws<RpcException>(() => _metadataClient.GetDeploymentMetadata(new GetDeploymentMetadataRequest
            {
                DeploymentId = "1234"
            }, authHeaders));

            Assert.AreEqual(response.Status.StatusCode, StatusCode.NotFound);
        }

        [Test]
        public void AllowForDeploymentMetadataAnnotationAndDeletionEntry()
        {
            var authHeaders = GetAuthenticationHeader();

            var updateDeploymentMetadataRequest = new UpdateDeploymentMetadataRequest()
            {
                DeploymentId = "1234",
                Metadata =
                {
                    {"random key 1", "random value 1"},
                    {"random key 2", "random value 2"}
                }
            };
            _metadataClient.UpdateDeploymentMetadata(updateDeploymentMetadataRequest, authHeaders);

            var deleteDeploymentMetadataRequest = new DeleteDeploymentMetadataEntryRequest()
            {
                DeploymentId = "1234",
                Key = "random key 1"
            };

            _metadataClient.DeleteDeploymentMetadataEntry(deleteDeploymentMetadataRequest, authHeaders);

            var response = _metadataClient.GetDeploymentMetadata(new GetDeploymentMetadataRequest
            {
                DeploymentId = "1234"
            }, authHeaders);

            CollectionAssert.AreEqual(response.Value, new Dictionary<string, string> { { "random key 2", "random value 2" } });
        }

        [Test]
        public void AllowForDeploymentMetadataAnnotationAndUpdate()
        {
            var authHeaders = GetAuthenticationHeader();

            var updateDeploymentMetadataRequest = new UpdateDeploymentMetadataRequest()
            {
                DeploymentId = "1234",
                Metadata =
                {
                    {"constant", "value"},
                    {"pending", "removal"},
                    {"updated", "shall be"}
                }
            };

            _metadataClient.UpdateDeploymentMetadata(updateDeploymentMetadataRequest, authHeaders);

            var updateOverrideDeploymentMetadataRequest = new UpdateDeploymentMetadataRequest()
            {
                DeploymentId = "1234",
                Metadata =
                {
                    {"pending", ""},        // Assigning an empty string should delete entry
                    {"updated", "it is"}
                }
            };

            _metadataClient.UpdateDeploymentMetadata(updateOverrideDeploymentMetadataRequest, authHeaders);

            var response =
                _metadataClient.GetDeploymentMetadata(new GetDeploymentMetadataRequest { DeploymentId = "1234" });

            CollectionAssert.AreEqual(response.Value, new Dictionary<string, string>
            {
                {"constant", "value"},
                {"updated", "it is"}
            });
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

        private Metadata GetAuthenticationHeader()
        {
            return new Metadata { { "x-auth-secret", _metadataServerSecret } };
        }
    }
}
