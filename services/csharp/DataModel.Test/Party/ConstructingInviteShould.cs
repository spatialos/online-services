using System.Collections.Generic;
using Improbable.OnlineServices.DataModel.Party;
using NUnit.Framework;

namespace Improbable.OnlineServices.DataModel.Test.Party
{
    [TestFixture]
    public class ConstructingInviteShould
    {
        private const string SenderId = "Hogwarts";
        private const string ReceiverId = "Harry";
        private const string PartyId = "FirstYear";

        private readonly IDictionary<string, string> _metadata = new Dictionary<string, string>
            {{"AllowedPets", "ToadCatOwl"}};

        [Test]
        public void SetIdBasedOnSenderReceiverAndPartyId()
        {
            var invite = new Invite(SenderId, ReceiverId, PartyId);
            Assert.AreEqual($"{SenderId}:{ReceiverId}:{PartyId}", invite.Id);
        }

        [Test]
        public void SetFieldsAccordingly()
        {
            var invite = new Invite(SenderId, ReceiverId, PartyId, _metadata);
            Assert.AreEqual(SenderId, invite.SenderId);
            Assert.AreEqual(ReceiverId, invite.ReceiverId);
            Assert.AreEqual(PartyId, invite.PartyId);
            Assert.AreEqual(Invite.Status.Pending, invite.CurrentStatus);
            CollectionAssert.AreEquivalent(_metadata, invite.Metadata);
        }

        [Test]
        public void SetMetadataToEmptyDictIfNotGiven()
        {
            var invite = new Invite(SenderId, ReceiverId, PartyId);
            CollectionAssert.IsEmpty(invite.Metadata);
        }
    }
}
