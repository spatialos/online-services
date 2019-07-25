using NUnit.Framework;
using PartyDataModel = Improbable.OnlineServices.DataModel.Party.Party;

namespace Improbable.OnlineServices.DataModel.Test.Party
{
    [TestFixture]
    public class PartyUpdateMinMaxMembersShould
    {
        private const string LeaderId = "Mordred";
        private const uint MinMembers = 100;
        private const uint MaxMembers = 200;

        private static PartyDataModel _party;

        [SetUp]
        public void SetUp()
        {
            _party = new PartyDataModel(LeaderId, "PIT", MinMembers, MaxMembers);
        }

        [Test]
        public void ReturnFalseIfMinMembersIsGreaterThanMaxMembers()
        {
            Assert.False(_party.UpdateMinMaxMembers(MaxMembers, MinMembers));
            Assert.AreEqual(MinMembers, _party.MinMembers);
            Assert.AreEqual(MaxMembers, _party.MaxMembers);
        }

        [Test]
        public void ReturnFalseIfMaxMembersIsLessThanCurrentNumberOfMembers()
        {
            _party.MemberIdToPit["New"] = "PIT";
            Assert.False(_party.UpdateMinMaxMembers(1, 1));
            Assert.AreEqual(MinMembers, _party.MinMembers);
            Assert.AreEqual(MaxMembers, _party.MaxMembers);
        }

        [Test]
        public void ReturnTrueIfSuccessful()
        {
            Assert.True(_party.UpdateMinMaxMembers(3, 5));
            Assert.AreEqual(3, _party.MinMembers);
            Assert.AreEqual(5, _party.MaxMembers);
        }
    }
}
