using System;
using Grpc.Core;
using Improbable.MetagameServices.Proto.Gateway;
using Improbable.SpatialOS.Deployment.V1Alpha1;
using Improbable.SpatialOS.Platform.Common;

namespace Improbable.MetagameServices.Base.Matcher
{
    public abstract class Matcher
    {
        private const string SpatialRefreshTokenEnvironmentVariable = "SPATIAL_REFRESH_TOKEN";
        private const string GatewayServiceTargetEnvironmentVariable = "GATEWAY_SERVICE_TARGET";
        private readonly GatewayInternalService.GatewayInternalServiceClient _gatewayClient;
        private readonly DeploymentServiceClient _spatialDeploymentClient;
        private volatile bool _running;

        protected Matcher()
        {
            var spatialRefreshToken = Environment.GetEnvironmentVariable(SpatialRefreshTokenEnvironmentVariable) ??
                                      throw new Exception(
                                          $"{SpatialRefreshTokenEnvironmentVariable} environment variable is required.");
            _spatialDeploymentClient =
                DeploymentServiceClient.Create(credentials: new PlatformRefreshTokenCredential(spatialRefreshToken));
            _gatewayClient =
                new GatewayInternalService.GatewayInternalServiceClient(new Channel(
                    Environment.GetEnvironmentVariable(GatewayServiceTargetEnvironmentVariable)
                    ?? throw new Exception(
                        $"Environment variable {GatewayServiceTargetEnvironmentVariable} is required."),
                    ChannelCredentials.Insecure));
        }

        public void Start()
        {
            _running = true;
            while (_running)
            {
                try
                {
                    DoMatch(_gatewayClient, _spatialDeploymentClient);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Uncaught exception, shutting down: {e.Message}:\n{e.StackTrace}");
                    _running = false;
                    break;
                }
            }

            DoShutdown(_gatewayClient, _spatialDeploymentClient);
        }

        public void Stop()
        {
            _running = false;
        }

        protected abstract void DoMatch(GatewayInternalService.GatewayInternalServiceClient gatewayClient,
            DeploymentServiceClient deploymentServiceClient);

        protected abstract void DoShutdown(GatewayInternalService.GatewayInternalServiceClient gatewayClient,
            DeploymentServiceClient deploymentServiceClient);
    }
}
