using NUnit.Framework;
using InviteDataModel = Improbable.MetagameServices.DataModel.Party.Invite;
using InviteStatusDataModel = Improbable.MetagameServices.DataModel.Party.Invite.Status;
using InviteProto = Improbable.MetagameServices.Proto.Invite.Invite;
using InviteStatusProto = Improbable.MetagameServices.Proto.Invite.Invite.Types.Status;

namespace Party.Test
{
    public static class InviteComparator
    {
        internal static void AssertEquivalent(InviteDataModel expected, InviteProto actual)
        {
            Assert.AreEqual(expected.Id, actual.Id);
            Assert.AreEqual(expected.SenderId, actual.SenderPlayerId);
            Assert.AreEqual(expected.ReceiverId, actual.ReceiverPlayerId);
            Assert.AreEqual(expected.PartyId, actual.PartyId);
            Assert.AreEqual(ConvertToProto(expected.CurrentStatus), actual.CurrentStatus);
            CollectionAssert.AreEquivalent(expected.Metadata, actual.Metadata);
        }

        private static InviteProto.Types.Status ConvertToProto(InviteDataModel.Status status)
        {
            switch (status)
            {
                case InviteStatusDataModel.Pending:
                    return InviteStatusProto.Pending;
                case InviteStatusDataModel.Accepted:
                    return InviteStatusProto.Accepted;
                case InviteStatusDataModel.Declined:
                    return InviteStatusProto.Declined;
                default:
                    return InviteStatusProto.Unknown;
            }
        }
    }
}
