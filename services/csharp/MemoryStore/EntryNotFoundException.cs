namespace MemoryStore
{
    public class EntryNotFoundException : MemoryStoreException
    {
        public string Id { get; }

        public EntryNotFoundException(string id, string message = null) : base(message ?? $"Entry {id} not found")
        {
            Id = id;
        }
    }
}
