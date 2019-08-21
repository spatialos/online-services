using System.Collections.Generic;

namespace Improbable.OnlineServices.DataModel.Metadata
{
    public class DeploymentMetadata : Entry
    {
        public DeploymentMetadata(string deploymentId, Dictionary<string, string> metadata = null)
        {
            Id = deploymentId;
            Metadata = metadata == null ? new Dictionary<string, string>() : new Dictionary<string, string>(metadata);
        }

        public IDictionary<string, string> Metadata { get; }
    }
}
