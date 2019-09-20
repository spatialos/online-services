using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.DataCollection;
using Newtonsoft.Json;

namespace Improbable.OnlineServices.DataModel.Gateway
{
    public class PlayerJoinRequest : Entry
    {
        public string MatchRequestId { get; }
        public string PartyId { get; private set; }

        public PlayerJoinRequest(string playerIdentity, string playerIdentityToken, string type,
            string matchRequestId, string partyId, Dictionary<string, string> metadata)
        {
            MatchRequestId = matchRequestId;
            PartyId = partyId;
            Id = playerIdentity;
            PlayerIdentity = playerIdentity;
            PlayerIdentityToken = playerIdentityToken;
            Type = type;
            Metadata = metadata;
            State = MatchState.Requested;
        }

        [JsonConstructor]
        public PlayerJoinRequest(string id, string playerIdentity, string playerIdentityToken, string type, Dictionary<string, string> metadata,
            MatchState state, string deploymentId, string deploymentName, string matchRequestId = null, string partyId = null)
        {
            MatchRequestId = matchRequestId ?? Guid.NewGuid().ToString();
            PartyId = partyId ?? Guid.NewGuid().ToString();
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
