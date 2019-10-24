using System;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Improbable.OnlineServices.Common;
using Improbable.OnlineServices.Proto.Gateway;
using Improbable.OnlineServices.Proto.Metadata;
using Improbable.SpatialOS.Deployment.V1Beta1;
using Improbable.SpatialOS.Platform.Common;

namespace Improbable.OnlineServices.Base.Matcher
{
    public abstract class Matcher
    {
        private const string SpatialRefreshTokenEnvironmentVariable = "SPATIAL_REFRESH_TOKEN";
        private const string GatewayServiceTargetEnvironmentVariable = "GATEWAY_SERVICE_TARGET";
        private static readonly string MetadataServiceTargetEnvironmentVariable = "METADATA_SERVICE_TARGET";
        private static readonly string MetadataServiceSecretEnvironmentVariable = "DEPLOYMENT_METADATA_SERVER_SECRET";
        private readonly GatewayInternalService.GatewayInternalServiceClient _gatewayClient;
        private readonly DeploymentServiceClient _spatialDeploymentClient;
        private readonly DeploymentMetadataService.DeploymentMetadataServiceClient _metadataClient;
        private volatile bool _running;

        protected Matcher()
        {
            var spatialRefreshToken = Environment.GetEnvironmentVariable(SpatialRefreshTokenEnvironmentVariable) ??
                                      throw new Exception(
                                          $"{SpatialRefreshTokenEnvironmentVariable} environment variable is required.");

            var metadataServiceTarget = Environment.GetEnvironmentVariable(MetadataServiceTargetEnvironmentVariable) ??
                                                  throw new Exception(
                                                      $"{MetadataServiceTargetEnvironmentVariable} environment variable is required.");
            if (string.IsNullOrEmpty(metadataServiceTarget))
            {
                throw new ArgumentException("Metadata service target should not be empty");
            }

            var metadataServerSecret = Secrets.GetEnvSecret(MetadataServiceSecretEnvironmentVariable);

            _spatialDeploymentClient = DeploymentServiceClient.Create(
                credentials: new PlatformRefreshTokenCredential(spatialRefreshToken)
            );
            _gatewayClient = new GatewayInternalService.GatewayInternalServiceClient(
                new Channel(
                    Environment.GetEnvironmentVariable(GatewayServiceTargetEnvironmentVariable) ??
                        throw new Exception($"Environment variable {GatewayServiceTargetEnvironmentVariable} is required."),
                    ChannelCredentials.Insecure
                )
            );
            _metadataClient = new DeploymentMetadataService.DeploymentMetadataServiceClient(
                new Channel(metadataServiceTarget, ChannelCredentials.Insecure).Intercept(
                    metadata =>
                    {
                        metadata.Add("x-auth-secret", metadataServerSecret);
                        return metadata;
                    }
                )
            );
        }

        public void Start()
        {
            _running = true;
            while (_running)
            {
                try
                {
                    DoMatch(_gatewayClient, _spatialDeploymentClient, _metadataClient);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Uncaught exception, shutting down: {e.Message}:\n{e.StackTrace}");
                    _running = false;
                    break;
                }
            }

            DoShutdown(_gatewayClient, _spatialDeploymentClient, _metadataClient);
        }

        public void Stop()
        {
            _running = false;
        }

        protected abstract void DoMatch(GatewayInternalService.GatewayInternalServiceClient gatewayClient,
            DeploymentServiceClient deploymentServiceClient, DeploymentMetadataService.DeploymentMetadataServiceClient metadataClient);

        protected abstract void DoShutdown(GatewayInternalService.GatewayInternalServiceClient gatewayClient,
            DeploymentServiceClient deploymentServiceClient, DeploymentMetadataService.DeploymentMetadataServiceClient metadataClient);
    }
}
