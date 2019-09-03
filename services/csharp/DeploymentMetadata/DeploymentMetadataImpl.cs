using System.Collections.Generic;
using System.Threading.Tasks;
using Grpc.Core;
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
            throw new RpcException(new Status(StatusCode.Unimplemented, "TODO"));
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
            using (var memClient = _memoryStoreClientManager.GetClient())
            using (var tx = memClient.CreateTransaction())
            {
                tx.DeleteHashEntry(request.DeploymentId, request.Key);
            }

            return Task.FromResult(new DeleteDeploymentMetadataEntryResponse());
        }
    }
}
