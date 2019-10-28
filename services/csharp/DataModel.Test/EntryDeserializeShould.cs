using System.Collections.Generic;
using Improbable.OnlineServices.DataModel.Gateway;
using Improbable.OnlineServices.DataModel.Party;
using Newtonsoft.Json;
using NUnit.Framework;
using PartyDataModel = Improbable.OnlineServices.DataModel.Party.Party;

namespace Improbable.OnlineServices.DataModel.Test
{
    [TestFixture]
    public class EntryDeserializeShould
    {
        [Test]
        public void ReturnAnEquivalentParty()
        {
            var metadata = new Dictionary<string, string> { { "pls", "thx" } };
            var party = new PartyDataModel("user", "pit", 100, 200, metadata)
            {
                CurrentPhase = PartyDataModel.Phase.Matchmaking
            };

            var serializedParty = JsonConvert.SerializeObject(party);
            var deserializedParty = JsonConvert.DeserializeObject<PartyDataModel>(serializedParty);

            Assert.AreEqual(party.Id, deserializedParty.Id);
            Assert.AreEqual(party.LeaderPlayerId, deserializedParty.LeaderPlayerId);
            Assert.AreEqual(party.MinMembers, deserializedParty.MinMembers);
            Assert.AreEqual(party.MaxMembers, deserializedParty.MaxMembers);
            CollectionAssert.AreEquivalent(party.Metadata, deserializedParty.Metadata);
            CollectionAssert.AreEquivalent(party.MemberIdToPit, new Dictionary<string, string> { { "user", "pit" } });
            Assert.AreEqual(party.CurrentPhase, deserializedParty.CurrentPhase);
        }

        [Test]
        public void ReturnAnEquivalentMember()
        {
            var member = new Member("user", "party_id");

            var serializedMember = JsonConvert.SerializeObject(member);
            var deserializedMember = JsonConvert.DeserializeObject<Member>(serializedMember);

            Assert.AreEqual(member.Id, deserializedMember.Id);
            Assert.AreEqual(member.PartyId, deserializedMember.PartyId);
        }

        [Test]
        public void ReturnAnEquivalentInvite()
        {
            var invite = new Invite("sender", "receiver", "party_id",
                new Dictionary<string, string> { { "timestamp", "100" } })
            { CurrentStatus = Invite.Status.Accepted };

            var serializedInvite = JsonConvert.SerializeObject(invite);
            var deserializedInvite = JsonConvert.DeserializeObject<Invite>(serializedInvite);

            Assert.AreEqual(invite.Id, deserializedInvite.Id);
            Assert.AreEqual(invite.SenderId, deserializedInvite.SenderId);
            Assert.AreEqual(invite.ReceiverId, deserializedInvite.ReceiverId);
            Assert.AreEqual(invite.PartyId, deserializedInvite.PartyId);
            Assert.AreEqual(invite.CurrentStatus, deserializedInvite.CurrentStatus);
            CollectionAssert.AreEquivalent(invite.Metadata, deserializedInvite.Metadata);
        }

        [Test]
        public void ReturnAnEquivalentPlayerInvites()
        {
            var playerInvites = new PlayerInvites("player");
            playerInvites.InboundInviteIds.Add("in1");
            playerInvites.InboundInviteIds.Add("in2");
            playerInvites.OutboundInviteIds.Add("out1");
            playerInvites.OutboundInviteIds.Add("out2");

            var serializedPlayerInvites = JsonConvert.SerializeObject(playerInvites);
            var deserializedPlayerInvites = JsonConvert.DeserializeObject<PlayerInvites>(serializedPlayerInvites);

            Assert.AreEqual(playerInvites.Id, deserializedPlayerInvites.Id);
            CollectionAssert.AreEquivalent(playerInvites.InboundInviteIds, deserializedPlayerInvites.InboundInviteIds);
            CollectionAssert.AreEquivalent(playerInvites.OutboundInviteIds,
                deserializedPlayerInvites.OutboundInviteIds);
        }

        [Test]
        public void ReturnAnEquivalentPartyJoinRequest()
        {
            var partyJoinRequest = new PartyJoinRequest(new PartyDataModel("Leader", "PIT"), "type",
                new Dictionary<string, string> { { "cmf", "cmz" } });
            var serializedPartyJoinRequest = JsonConvert.SerializeObject(partyJoinRequest);
            var deserializedPartyJoinRequest =
                JsonConvert.DeserializeObject<PartyJoinRequest>(serializedPartyJoinRequest);

            Assert.AreEqual(partyJoinRequest.Id, deserializedPartyJoinRequest.Id);
            Assert.AreEqual(partyJoinRequest.Party.Id, deserializedPartyJoinRequest.Party.Id);
            Assert.AreEqual(partyJoinRequest.Type, deserializedPartyJoinRequest.Type);
            Assert.AreEqual(0, deserializedPartyJoinRequest.Score);
            Assert.IsNull(deserializedPartyJoinRequest.QueueName);
            CollectionAssert.AreEquivalent(partyJoinRequest.Metadata, deserializedPartyJoinRequest.Metadata);
        }

        [Test]
        public void ReturnAnEquivalentPlayerJoinRequest()
        {
            var playerJoinRequest =
                new PlayerJoinRequest("Leader", "PIT", "type", "MatchRequestId", "partyId", new Dictionary<string, string> { { "cmf", "cmz" } });
            playerJoinRequest.AssignMatch("deployment-id", "deployment-name");
            var serializedPlayerJoinRequest = JsonConvert.SerializeObject(playerJoinRequest);
            var deserializedPlayerJoinRequest =
                JsonConvert.DeserializeObject<PlayerJoinRequest>(serializedPlayerJoinRequest);

            Assert.AreEqual(playerJoinRequest.Id, deserializedPlayerJoinRequest.Id);
            Assert.AreEqual(playerJoinRequest.PlayerIdentity, deserializedPlayerJoinRequest.PlayerIdentity);
            Assert.AreEqual(playerJoinRequest.PlayerIdentityToken, deserializedPlayerJoinRequest.PlayerIdentityToken);
            Assert.AreEqual(playerJoinRequest.DeploymentName, deserializedPlayerJoinRequest.DeploymentName);
            Assert.AreEqual(playerJoinRequest.DeploymentId, deserializedPlayerJoinRequest.DeploymentId);
            Assert.AreEqual(playerJoinRequest.Type, deserializedPlayerJoinRequest.Type);
            Assert.AreEqual(playerJoinRequest.State, deserializedPlayerJoinRequest.State);
            CollectionAssert.AreEquivalent(playerJoinRequest.Metadata, deserializedPlayerJoinRequest.Metadata);
        }
    }
}
