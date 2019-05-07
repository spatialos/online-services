using NUnit.Framework;
using PartyDataModel = Improbable.OnlineServices.DataModel.Party.Party;

namespace Improbable.OnlineServices.DataModel.Test.Party
{
    [TestFixture]
    public class PartyUpdateLeaderShould
    {
        private const string LeaderId = "Mordred";

        private static PartyDataModel _party;

        [SetUp]
        public void SetUp()
        {
            _party = new PartyDataModel(LeaderId, "PIT");
        }

        [Test]
        public void ReturnFalseIfProposedLeaderIsNotAMember()
        {
            Assert.False(_party.UpdatePartyLeader("Percival"));
        }

        [Test]
        public void UpdateFieldsThatAreGiven()
        {
            _party.MemberIdToPit["Oberon"] = "PIT";

            Assert.True(_party.UpdatePartyLeader("Oberon"));
            Assert.AreEqual("Oberon", _party.LeaderPlayerId);
        }
    }
}