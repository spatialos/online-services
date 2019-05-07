using System;
using Improbable.OnlineServices.DataModel;

namespace MemoryStore
{
    /// <summary>
    /// Defines the general operations which can be done on a memory store. For every use of a memory client, create a
    /// instance as follows:
    /// <code>
    /// using (var memClient = ... )
    /// {
    ///     // perform operations
    /// }
    /// </code>
    /// </summary>
    public interface IMemoryStoreClient : IDisposable
    {
        /// <returns>
        /// A new <see cref="ITransaction"/>, which can be used to perform several operations atomically on the memory
        /// store.
        /// </returns>
        ITransaction CreateTransaction();

        /// <returns>
        /// Returns the <see cref="Entry"/> associated with the given id or null if it doesn't exist.
        /// </returns>
        T Get<T>(string id) where T : Entry;
    }
}