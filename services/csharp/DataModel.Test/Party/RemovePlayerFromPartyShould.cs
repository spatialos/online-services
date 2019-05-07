using System;
using NUnit.Framework;
using PartyDataModel = Improbable.OnlineServices.DataModel.Party.Party;

namespace Improbable.OnlineServices.DataModel.Test.Party
{
    [TestFixture]
    public class RemovePlayerFromPartyShould
    {
        private const string LeaderId = "Mordred";
        private const string PlayerId = "Oberon";

        private static PartyDataModel _party;

        [SetUp]
        public void SetUp()
        {
            _party = new PartyDataModel(LeaderId, "PIT");
            _party.AddPlayerToParty(PlayerId, "PIT");
        }

        [Test]
        public void ReturnFalseIfPlayerIsNotAMember()
        {
            Assert.False(_party.RemovePlayerFromParty("NotAMember"));
        }

        [Test]
        public void ReturnTrueIfSuccessfullyRemovedPlayer()
        {
            Assert.True(_party.RemovePlayerFromParty(PlayerId));
            Assert.Null(_party.GetMember(PlayerId));
        }

        [Test]
        public void RandomlyAssignNewLeaderWhenThereAreStillPlayersInTheParty()
        {
            Assert.True(_party.RemovePlayerFromParty(LeaderId));

            // Since there is only one other member in the party, they should automatically be assigned the leader role.
            var newLeader = _party.GetLeader();
            Assert.NotNull(newLeader);
            Assert.AreEqual(PlayerId, newLeader.Id);
        }

        [Test]
        public void ThrowExceptionIfTryingToRemoveLastPlayer()
        {
            _party.RemovePlayerFromParty(PlayerId);
            var exception = Assert.Throws<Exception>(() => _party.RemovePlayerFromParty(LeaderId));
            Assert.That(exception.Message, Contains.Substring("last member"));
        }
    }
}