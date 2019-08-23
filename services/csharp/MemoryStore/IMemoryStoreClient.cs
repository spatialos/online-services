using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
        Task<T> GetAsync<T>(string id) where T : Entry;


        /// <summary>
        /// Returns all fields and values of the hash stored at key.
        /// </summary>
        /// <param name="key">The key of the hash to get all entries from.</param>
        /// <returns>Dictionary of fields and their values stored in the hash.</returns>
        Task<IDictionary<string, string>> GetHashAsync(string key);

        /// <summary>
        /// Returns the value associated with field in the hash stored at key.
        /// </summary>
        /// <param name="key">The key of the hash.</param>
        /// <param name="hashField">The field in the hash to get.</param>
        /// <returns>The value associated with field.</returns>
        Task<string> GetHashEntryAsync(string key, string hashField);
    }
}
