using System.Collections.Generic;
using Newtonsoft.Json;

namespace Improbable.MetagameServices.DataModel.Party
{
    public class PlayerInvites : Entry
    {
        public PlayerInvites(string playerId)
        {
            Id = playerId;
            OutboundInviteIds = new HashSet<string>();
            InboundInviteIds = new HashSet<string>();
        }

        [JsonConstructor]
        public PlayerInvites(string id, ISet<string> inboundInviteIds, ISet<string> outboundInviteIds)
        {
            Id = id;
            InboundInviteIds = inboundInviteIds;
            OutboundInviteIds = outboundInviteIds;
        }

        public ISet<string> OutboundInviteIds { get; }
        public ISet<string> InboundInviteIds { get; }
    }
}
