namespace MemoryStore
{
    /// <summary>
    /// Signals that an error has occurred while committing a transaction to the memory store.
    /// </summary>
    public class TransactionAbortedException : MemoryStoreException
    {
        public TransactionAbortedException()
        {
        }

        public TransactionAbortedException(string message) : base(message)
        {
        }
    }
}
