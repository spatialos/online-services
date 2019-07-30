using NUnit.Framework;
using PartyDataModel = Improbable.OnlineServices.DataModel.Party.Party;

namespace Improbable.OnlineServices.DataModel.Test.Party
{
    [TestFixture]
    public class GetMemberShould
    {
        private const string TestLeaderId = "Leader";
        private static readonly PartyDataModel _party = new PartyDataModel(TestLeaderId, "DUMMY_PIT");

        [Test]
        public void ReturnNullIfPlayerIsNotAMemberOfTheParty()
        {
            Assert.Null(_party.GetMember("NotAMember"));
        }

        [Test]
        public void ReturnMemberEntryIfPlayerIsAMember()
        {
            var member = _party.GetMember(TestLeaderId);
            Assert.NotNull(member);
            Assert.AreEqual(TestLeaderId, member.Id);
            Assert.AreEqual(_party.Id, member.PartyId);
            Assert.AreEqual(member.SerializeToJson(), member.PreviousState);
        }
    }
}
