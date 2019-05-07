namespace MemoryStore
{
    public class EntryAlreadyExistsException : MemoryStoreException
    {
        public string Id { get; }

        public EntryAlreadyExistsException(string id, string message = null) :
            base(message ?? $"Entry {id} already exists")
        {
            Id = id;
        }
    }
}