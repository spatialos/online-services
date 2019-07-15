using NUnit.Framework;
using InviteDataModel = Improbable.MetagameServices.DataModel.Party.Invite;

namespace Improbable.MetagameServices.DataModel.Test.Party
{
    [TestFixture]
    public class InvitePlayerInvolvedShould
    {
        private const string SenderId = "Hogwarts";
        private const string ReceiverId = "Harry";
        private const string PartyId = "FirstYear";

        [Test]
        public void ReturnTrueIfPlayerIsEitherSenderOrReceiver()
        {
            var invite = new InviteDataModel(SenderId, ReceiverId, PartyId);
            Assert.True(invite.PlayerInvolved(SenderId));
            Assert.True(invite.PlayerInvolved(ReceiverId));
        }

        [Test]
        public void ReturnFalseIfIsNeitherSenderNorReceiver()
        {
            var invite = new InviteDataModel(SenderId, ReceiverId, PartyId);
            Assert.False(invite.PlayerInvolved("SomeoneElse"));
        }
    }
}
