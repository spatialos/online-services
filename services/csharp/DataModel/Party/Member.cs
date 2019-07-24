namespace Improbable.MetagameServices.DataModel.Party
{
    public class Member : Entry
    {
        public Member(string id, string partyId)
        {
            Id = id;
            PartyId = partyId;
        }

        public string PartyId { get; }
    }
}
