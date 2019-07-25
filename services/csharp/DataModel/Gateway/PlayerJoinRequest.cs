using System.Collections.Generic;
using Newtonsoft.Json;

namespace Improbable.OnlineServices.DataModel.Gateway
{
    public class PlayerJoinRequest : Entry
    {
        public PlayerJoinRequest(string playerIdentity, string playerIdentityToken, string type,
            Dictionary<string, string> metadata)
        {
            Id = playerIdentity;
            PlayerIdentity = playerIdentity;
            PlayerIdentityToken = playerIdentityToken;
            Type = type;
            Metadata = metadata;
            State = MatchState.Requested;
        }

        [JsonConstructor]
        public PlayerJoinRequest(string id, string playerIdentity, string playerIdentityToken, string type,
            Dictionary<string, string> metadata, MatchState state, string deploymentId, string deploymentName)
        {
            Id = playerIdentity;
            PlayerIdentity = playerIdentity;
            PlayerIdentityToken = playerIdentityToken;
            Type = type;
            Metadata = metadata;
            State = state;
            DeploymentId = deploymentId;
            DeploymentName = deploymentName;
        }

        public string PlayerIdentity { get; }

        public string PlayerIdentityToken { get; }

        public string Type { get; }

        public Dictionary<string, string> Metadata { get; }

        public MatchState State { get; set; }

        public string DeploymentId { get; private set; }

        public string DeploymentName { get; private set; }

        public void AssignMatch(string deploymentId, string deploymentName)
        {
            State = MatchState.Matched;
            DeploymentId = deploymentId;
            DeploymentName = deploymentName;
        }

        public bool IsComplete()
        {
            return State == MatchState.Error || State == MatchState.Matched;
        }
    }
}
