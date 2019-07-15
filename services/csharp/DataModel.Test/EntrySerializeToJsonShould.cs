using Improbable.MetagameServices.DataModel.Party;
using NUnit.Framework;
using PartyDataModel = Improbable.MetagameServices.DataModel.Party.Party;

namespace Improbable.MetagameServices.DataModel.Test
{
    [TestFixture]
    public class EntrySerializeToJsonShould
    {
        [Test]
        public void IgnorePreviousStateField()
        {
            var member = new Member("IAmPlayerWoo", "PartyId")
            {
                PreviousState = "IAmPreviousStateWoo"
            };

            var serializedValue = member.SerializeToJson();
            Assert.That(serializedValue, Does.Not.Contain("PreviousState"));
            Assert.That(serializedValue, Does.Not.Contain("IAmPreviousStateWoo"));
        }
    }
}
