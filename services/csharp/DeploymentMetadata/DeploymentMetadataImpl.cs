using System.Collections.Generic;
using System.Threading.Tasks;
using Grpc.Core;
using Improbable.OnlineServices.Common;
using Improbable.OnlineServices.Proto.Metadata;
using MemoryStore;

namespace DeploymentMetadata
{
    public class DeploymentMetadataImpl : DeploymentMetadataService.DeploymentMetadataServiceBase
    {
        private readonly IMemoryStoreClientManager<IMemoryStoreClient> _memoryStoreClientManager;

        public DeploymentMetadataImpl(
            IMemoryStoreClientManager<IMemoryStoreClient> memoryStoreClientManager)
        {
            _memoryStoreClientManager = memoryStoreClientManager;
        }

        public override Task<UpdateDeploymentMetadataResponse> UpdateDeploymentMetadata(
            UpdateDeploymentMetadataRequest request, ServerCallContext context)
        {
            AuthHeaders.CheckRequestAuthenticated(context);

            using (var memClient = _memoryStoreClientManager.GetClient())
            using (var tx = memClient.CreateTransaction())
            {
                tx.UpdateHashWithEntries(request.DeploymentId, request.Metadata);
            }

            return Task.FromResult(new UpdateDeploymentMetadataResponse());
        }

        public override Task<SetDeploymentMetadataEntryResponse> SetDeploymentMetadataEntry(
            SetDeploymentMetadataEntryRequest request, ServerCallContext context)
        {
            AuthHeaders.CheckRequestAuthenticated(context);

            if (string.IsNullOrEmpty(request.DeploymentId))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument,
                    $"request parameter {nameof(request.DeploymentId)} may not be empty"));
            }

            if (string.IsNullOrEmpty(request.Key))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument,
                    $"request parameter {nameof(request.Key)} may not be empty"));
            }

            using (var memClient = _memoryStoreClientManager.GetClient())
            using (var tx = memClient.CreateTransaction())
            {
                switch (request.Condition.Function)
                {
                    case Condition.Types.Function.NoCondition:
                        break;
                    case Condition.Types.Function.Exists:
                        tx.AddHashEntryExistsCondition(request.DeploymentId, request.Key);
                        break;
                    case Condition.Types.Function.NotExists:
                        tx.AddHashEntryNotExistsCondition(request.DeploymentId, request.Key);
                        break;
                    case Condition.Types.Function.Equal:
                        tx.AddHashEntryEqualCondition(request.DeploymentId, request.Key, request.Condition.Payload);
                        break;
                    case Condition.Types.Function.NotEqual:
                        tx.AddHashEntryNotEqualCondition(request.DeploymentId, request.Key, request.Condition.Payload);
                        break;
                    default:
                        throw new RpcException(new Status(StatusCode.InvalidArgument,
                            $"an invalid condition '{request.Condition.Function}' was provided"));
                }

                tx.UpdateHashWithEntries(request.DeploymentId, new[]
                {
                    new KeyValuePair<string, string>(request.Key, request.Value)
                });
            }

            return Task.FromResult(new SetDeploymentMetadataEntryResponse());
        }

        public override async Task<GetDeploymentMetadataResponse> GetDeploymentMetadata(
            GetDeploymentMetadataRequest request, ServerCallContext context)
        {
            using (var memClient = _memoryStoreClientManager.GetClient())
            {
                var metadata = await memClient.GetHashAsync(request.DeploymentId) ??
                               throw new RpcException(new Status(StatusCode.NotFound,
                                   $"Metadata for deployment ID {request.DeploymentId} doesn't exist."));

                return new GetDeploymentMetadataResponse
                {
                    Value = { metadata }
                };
            }
        }

        public override async Task<GetDeploymentMetadataEntryResponse> GetDeploymentMetadataEntry(
            GetDeploymentMetadataEntryRequest request, ServerCallContext context)
        {
            using (var memClient = _memoryStoreClientManager.GetClient())
            {
                var metadataValue = await memClient.GetHashEntryAsync(request.DeploymentId, request.Key) ??
                                    throw new RpcException(new Status(StatusCode.NotFound,
                                        $"Metadata entry for deployment ID {request.DeploymentId} and key {request.Key} doesn't exist."));

                return new GetDeploymentMetadataEntryResponse
                {
                    Value = metadataValue
                };
            }
        }

        public override Task<DeleteDeploymentMetadataResponse> DeleteDeploymentMetadata(
            DeleteDeploymentMetadataRequest request, ServerCallContext context)
        {
            AuthHeaders.CheckRequestAuthenticated(context);

            using (var memClient = _memoryStoreClientManager.GetClient())
            using (var tx = memClient.CreateTransaction())
            {
                tx.DeleteKey(request.DeploymentId);
            }

            return Task.FromResult(new DeleteDeploymentMetadataResponse());
        }

        public override Task<DeleteDeploymentMetadataEntryResponse> DeleteDeploymentMetadataEntry(
            DeleteDeploymentMetadataEntryRequest request, ServerCallContext context)
        {
            AuthHeaders.CheckRequestAuthenticated(context);

            using (var memClient = _memoryStoreClientManager.GetClient())
            using (var tx = memClient.CreateTransaction())
            {
                tx.DeleteHashEntry(request.DeploymentId, request.Key);
            }

            return Task.FromResult(new DeleteDeploymentMetadataEntryResponse());
        }
    }
}
