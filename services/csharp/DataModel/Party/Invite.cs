using System.Collections.Generic;

namespace Improbable.OnlineServices.DataModel.Party
{
    public class Invite : Entry
    {
        public enum Status
        {
            Unknown,
            Pending,
            Accepted,
            Declined
        };

        // The format is <sender_id>:<receiver_id>:<party_id>.
        private const string InviteIdFormat = "{0}:{1}:{2}";

        public Invite(string senderId, string receiverId, string partyId, IDictionary<string, string> metadata = null)
        {
            Id = string.Format(InviteIdFormat, senderId, receiverId, partyId);
            SenderId = senderId;
            ReceiverId = receiverId;
            PartyId = partyId;
            Metadata = metadata == null ? new Dictionary<string, string>() : new Dictionary<string, string>(metadata);
            CurrentStatus = Status.Pending;
        }

        public string SenderId { get; }
        public string ReceiverId { get; }
        public string PartyId { get; }
        public IDictionary<string, string> Metadata { get; }
        public Status CurrentStatus { get; set; }

        public bool PlayerInvolved(string playerId)
        {
            return playerId == SenderId || playerId == ReceiverId;
        }

        public void UpdateMetadata(IDictionary<string, string> updates)
        {
            MetadataUpdater.Update(Metadata, updates);
        }
    }
}
