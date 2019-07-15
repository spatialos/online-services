using Improbable.MetagameServices.DataModel.Party;
using NUnit.Framework;

namespace Improbable.MetagameServices.DataModel.Test.Party
{
    [TestFixture]
    public class ConstructingMemberShould
    {
        private const string TestPlayerId = "Cornelius";
        private const string TestPartyId = "Ministry2020";

        [Test]
        public void SetAllFields()
        {
            var member = new Member(TestPlayerId, TestPartyId);

            Assert.AreEqual(TestPlayerId, member.Id);
            Assert.AreEqual(TestPartyId, member.PartyId);
            Assert.Null(member.PreviousState);
        }
    }
}
