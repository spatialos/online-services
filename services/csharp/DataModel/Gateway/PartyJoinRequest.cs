using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using PartyDataModel = Improbable.OnlineServices.DataModel.Party.Party;

namespace Improbable.OnlineServices.DataModel.Gateway
{
    public class PartyJoinRequest : QueuedEntry
    {
        public PartyJoinRequest(PartyDataModel party, string type, Dictionary<string, string> metadata)
        {
            Id = party.Id;
            Party = new PartyDataModel(party);
            Type = type;
            Metadata = metadata;
            RefreshQueueData();
        }

        [JsonConstructor]
        public PartyJoinRequest(string id, PartyDataModel party, string type, Dictionary<string, string> metadata,
            string queueName, double score)
        {
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