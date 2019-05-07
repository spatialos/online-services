using StackExchange.Redis;
using Improbable.OnlineServices.DataModel;
using Newtonsoft.Json;

namespace MemoryStore.Redis
{
    public class RedisClient : IMemoryStoreClient
    {
        private readonly IDatabase _internalClient;
        private readonly LoadedLuaScript _zpopMinScript;

        public RedisClient(IDatabase client, LoadedLuaScript zpopMinScript)
        {
            _internalClient = client;
            _zpopMinScript = zpopMinScript;
        }

        public ITransaction CreateTransaction()
        {
            return new RedisTransaction(_internalClient.CreateTransaction(), _zpopMinScript);
        }

        public T Get<T>(string id) where T : Entry
        {
            var key = Key.For<T>(id);
            var serializedEntry = _internalClient.StringGet(key);
            if (serializedEntry.IsNullOrEmpty)
            {
                return null;
            }

            var entry = JsonConvert.DeserializeObject<T>(serializedEntry);
            entry.PreviousState = serializedEntry;
            return entry;
        }

        public void Dispose()
        {
        }
    }
}