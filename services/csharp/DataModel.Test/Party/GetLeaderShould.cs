using System.Collections.Generic;
using NUnit.Framework;
using PartyDataModel = Improbable.MetagameServices.DataModel.Party.Party;

namespace Improbable.MetagameServices.DataModel.Test.Party
{
    [TestFixture]
    public class GetLeaderShould
    {
        [Test]
        public void ReturnLeaderWhenThereLeaderPlayerIdIsSet()
        {
            var party = new PartyDataModel("Leader", "PIT", 10, 20, new Dictionary<string, string>());
            var member = party.GetLeader();

            Assert.NotNull(member);
            Assert.AreEqual("Leader", member.Id);
            Assert.AreEqual(party.Id, member.PartyId);
            Assert.AreEqual(member.SerializeToJson(), member.PreviousState);
        }
    }
}
