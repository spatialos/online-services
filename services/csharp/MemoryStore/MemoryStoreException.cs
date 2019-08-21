using System;

namespace MemoryStore
{
    /// <summary>
    /// Signals that an error has occurred while performing operations on the memory store.
    /// </summary>
    public class MemoryStoreException : Exception
    {
        public MemoryStoreException()
        {
        }

        public MemoryStoreException(string errorMessage) : base(errorMessage)
        {
        }
    }
}
