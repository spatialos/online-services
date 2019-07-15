using System;
using NUnit.Framework;
using PartyDataModel = Improbable.MetagameServices.DataModel.Party.Party;

namespace Improbable.MetagameServices.DataModel.Test.Party
{
    [TestFixture]
    public class AddPlayerToPartyShould
    {
        private const string LeaderId = "Mordred";
        private const string PlayerId = "Oberon";

        private static PartyDataModel _party;

        [SetUp]
        public void SetUp()
        {
            _party = new PartyDataModel(LeaderId, "PIT");
        }

        [Test]
        public void ReturnFalseIfPlayerIsAlreadyAMember()
        {
            Assert.False(_party.AddPlayerToParty(LeaderId, "PIT"));
        }

        [Test]
        public void ReturnExceptionWhenPartyIsAtFullCapacity()
        {
            _party.UpdateMinMaxMembers(1, 1);
            var exception = Assert.Throws<Exception>(() => _party.AddPlayerToParty(PlayerId, "PIT"));
            Assert.That(exception.Message, Contains.Substring("full capacity"));
        }

        [Test]
        public void ReturnTrueIfSuccessfullyAddedPlayer()
        {
            Assert.True(_party.AddPlayerToParty(PlayerId, "PIT"));
            Assert.NotNull(_party.GetMember(PlayerId));
        }
    }
}
