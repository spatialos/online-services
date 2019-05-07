using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Improbable.OnlineServices.DataModel.Party
{
    public class Party : Entry
    {
        // Defaults for the minimum and maximum no. of members of a Party can be set using PartyServerCommandLineArgs.
        // At server start-up these can be manually set and will be server-specific defaults.
        // The command line args parsing framework doesn't accept unsigned integers as arguments, hence the maximum num
        // of members has an upper limit of int.MaxValue.
        public static class Defaults
        {
            public static uint MinMembers { get; set; } = 1;
            public static uint MaxMembers { get; set; } = int.MaxValue;
        }

        public Party(string leaderPlayerId, string leaderPit, uint minMembers = 0, uint maxMembers = 0,
            IDictionary<string, string> metadata = null)
        {
            Id = Guid.NewGuid().ToString();
            LeaderPlayerId = leaderPlayerId;
            MinMembers = minMembers == 0 ? Defaults.MinMembers : minMembers;
            MaxMembers = maxMembers == 0 ? Defaults.MaxMembers : maxMembers;

            if (MinMembers > MaxMembers)
            {
                throw new ArgumentException(
                    "The minimum number of members cannot be higher than the maximum number of members.");
            }

            Metadata = metadata ?? new Dictionary<string, string>();
            MemberIdToPit = new Dictionary<string, string> {{leaderPlayerId, leaderPit}};
            CurrentPhase = Phase.Forming;
        }

        public Party(Party other) : this(other.Id, other.LeaderPlayerId, other.MinMembers, other.MaxMembers,
            other.Metadata, other.MemberIdToPit, other.CurrentPhase, other.PreviousState)
        {
        }

        [JsonConstructor]
        public Party(string id, string leaderPlayerId, uint minMembers, uint maxMembers,
            IDictionary<string, string> metadata, IDictionary<string, string> memberIdToPit, Phase currentPhase,
            string previousState)
        {
            Id = id;
            LeaderPlayerId = leaderPlayerId;
            MinMembers = minMembers;
            MaxMembers = maxMembers;
            Metadata = new Dictionary<string, string>(metadata);
            MemberIdToPit = new Dictionary<string, string>(memberIdToPit);
            CurrentPhase = currentPhase;
            PreviousState = previousState;
        }

        public enum Phase
        {
            Unknown,
            Forming,
            Matchmaking,
            InGame
        }

        public string LeaderPlayerId { get; private set; }

        public uint MinMembers { get; private set; }

        public uint MaxMembers { get; private set; }

        public IDictionary<string, string> MemberIdToPit { get; }

        public IDictionary<string, string> Metadata { get; }

        public Phase CurrentPhase { get; set; }

        public Member GetLeader()
        {
            if (string.IsNullOrEmpty(LeaderPlayerId))
            {
                return null;
            }

            return GetMember(LeaderPlayerId);
        }

        public IEnumerable<Member> GetMembers()
        {
            var members = new List<Member>();
            foreach (var memberId in MemberIdToPit.Keys)
            {
                var member = new Member(memberId, Id);
                member.PreviousState = member.SerializeToJson();
                members.Add(member);
            }

            return members;
        }

        public Member GetMember(string playerId)
        {
            if (!MemberIdToPit.ContainsKey(playerId))
            {
                return null;
            }

            var member = new Member(playerId, Id);
            member.PreviousState = member.SerializeToJson();
            return member;
        }

        public bool UpdatePartyLeader(string proposedLeader)
        {
            if (!MemberIdToPit.ContainsKey(proposedLeader))
            {
                return false;
            }

            LeaderPlayerId = proposedLeader;
            return true;
        }

        public bool UpdateMinMaxMembers(uint minMembers, uint maxMembers)
        {
            if (minMembers > maxMembers)
            {
                return false;
            }

            if (maxMembers < MemberIdToPit.Count)
            {
                return false;
            }

            MinMembers = minMembers;
            MaxMembers = maxMembers;
            return true;
        }

        public void UpdateMetadata(IDictionary<string, string> updates)
        {
            MetadataUpdater.Update(Metadata, updates);
        }

        public bool AddPlayerToParty(string playerId, string pit)
        {
            if (MemberIdToPit.ContainsKey(playerId))
            {
                return false;
            }

            if (IsAtFullCapacity())
            {
                throw new Exception("The party is at full capacity");
            }

            MemberIdToPit[playerId] = pit;
            return true;
        }

        private bool IsAtFullCapacity()
        {
            return MemberIdToPit.Count == MaxMembers;
        }


        public bool RemovePlayerFromParty(string playerId)
        {
            if (!MemberIdToPit.ContainsKey(playerId))
            {
                return false;
            }

            if (MemberIdToPit.Count == 1)
            {
                throw new Exception("Cannot remove player if last member of the party");
            }

            MemberIdToPit.Remove(playerId);

            if (LeaderPlayerId == playerId)
            {
                LeaderPlayerId = MemberIdToPit.ElementAt(0).Key;
            }

            return true;
        }

        /// <returns>
        /// Whether the current number of members is above the lower limit of members that the party needs to have.
        /// </returns>
        public bool SufficientMembers()
        {
            return MemberIdToPit.Count >= MinMembers;
        }
    }
}