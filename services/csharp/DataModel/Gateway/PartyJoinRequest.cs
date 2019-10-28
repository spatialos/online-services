using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.DataCollection;
using Newtonsoft.Json;
using PartyDataModel = Improbable.OnlineServices.DataModel.Party.Party;

namespace Improbable.OnlineServices.DataModel.Gateway
{
    public class PartyJoinRequest : QueuedEntry
    {
        public string MatchRequestId { get; }
        public string PartyId { get; private set; }

        public PartyJoinRequest(PartyDataModel party, string type, Dictionary<string, string> metadata)
        {
            MatchRequestId = Guid.NewGuid().ToString();
            Id = party.Id;
            Party = new PartyDataModel(party);
            Type = type;
            Metadata = metadata;
            RefreshQueueData();
        }

        [JsonConstructor]
        public PartyJoinRequest(string id, PartyDataModel party, string type, Dictionary<string, string> metadata,
            string queueName, double score, string matchRequestId = null)
        {
            MatchRequestId = matchRequestId ?? Guid.NewGuid().ToString();
            Id = party.Id;
            Party = party;
            Type = type;
            Metadata = metadata;
            QueueName = queueName;
            Score = score;
        }

        public string Type { get; }

        public Dictionary<string, string> Metadata { get; }

        public PartyDataModel Party { get; }

        public void RefreshQueueData()
        {
            QueueName = Type;
            Score = (DateTime.UtcNow - DateTime.UnixEpoch).TotalMilliseconds;
        }
    }
}
