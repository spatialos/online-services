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
            throw new RpcException(new Status(StatusCode.Unimplemented, "TODO"));
        }

        public override Task<SetDeploymentMetadataEntryResponse> SetDeploymentMetadataEntry(
            SetDeploymentMetadataEntryRequest request, ServerCallContext context)
        {
            throw new RpcException(new Status(StatusCode.Unimplemented, "TODO"));
        }

        public override Task<GetDeploymentMetadataResponse> GetDeploymentMetadata(
            GetDeploymentMetadataRequest request, ServerCallContext context)
        {
            throw new RpcException(new Status(StatusCode.Unimplemented, "TODO"));
        }

        public override Task<GetDeploymentMetadataEntryResponse> GetDeploymentMetadataEntry(
            GetDeploymentMetadataEntryRequest request, ServerCallContext context)
        {
            throw new RpcException(new Status(StatusCode.Unimplemented, "TODO"));
        }

        public override Task<DeleteDeploymentMetadataResponse> DeleteDeploymentMetadata(
            DeleteDeploymentMetadataRequest request, ServerCallContext context)
        {
            throw new RpcException(new Status(StatusCode.Unimplemented, "TODO"));
        }

        public override Task<DeleteDeploymentMetadataEntryResponse> DeleteDeploymentMetadataEntry(
            DeleteDeploymentMetadataEntryRequest request, ServerCallContext context)
        {
            throw new RpcException(new Status(StatusCode.Unimplemented, "TODO"));
        }
    }
}
