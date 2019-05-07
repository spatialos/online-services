using System.Linq;
using NUnit.Framework;
using PartyDataModel = Improbable.OnlineServices.DataModel.Party.Party;

namespace Improbable.OnlineServices.DataModel.Test.Party
{
    [TestFixture]
    public class GetMembersShould
    {
        private const string TestLeaderId = "Lupin";
        private const string TestMemberId = "Fawkes";
        private const string DefaultPit = "DUMMY_PIT";

        [Test]
        public void ReturnACollectionWithAllMembers()
        {
            var party = new PartyDataModel(TestLeaderId, DefaultPit) {MemberIdToPit = {[TestMemberId] = DefaultPit}};
            var memberList = party.GetMembers().ToList();

            Assert.AreEqual(2, memberList.Count);

            var first = memberList[0];
            Assert.AreEqual(TestLeaderId, first.Id);
            Assert.AreEqual(party.Id, first.PartyId);
            Assert.AreEqual(first.SerializeToJson(), first.PreviousState);

            var second = memberList[1];
            Assert.AreEqual(TestMemberId, second.Id);
            Assert.AreEqual(party.Id, second.PartyId);
            Assert.AreEqual(second.SerializeToJson(), second.PreviousState);
        }
    }
}