using StackExchange.Redis;
using System;
using System.Diagnostics;

namespace MemoryStore.Redis
{
    public class RedisClientManager : IMemoryStoreClientManager<IMemoryStoreClient>, IDisposable
    {
        private const string LuaZPOPMIN = @"local range = redis.call('zrange', @key, 0, @count - 1, 'WITHSCORES')
local ret = {}
local member
for i, v in ipairs(range) do
    if i % 2 == 1 then
        member = v
    else
        table.insert(ret, {member, tonumber(v)})
    end
end
redis.call('zremrangebyrank', @key, 0, @count - 1)
return ret";

        public enum Database { DEFAULT = 0, CACHE = 1 }
        private readonly ConnectionMultiplexer _connectionMultiplexer;
        private readonly LoadedLuaScript _loadedZpopminScript;

        public RedisClientManager(string connectionString)
        {
            _connectionMultiplexer = ConnectionMultiplexer.Connect(connectionString);
            var prepared = LuaScript.Prepare(LuaZPOPMIN);
            _loadedZpopminScript = prepared.Load(_connectionMultiplexer.GetServer(connectionString));
        }

        public IMemoryStoreClient GetClient()
        {
            return new RedisClient(_connectionMultiplexer.GetDatabase(), _loadedZpopminScript);
        }

        public IDatabase GetRawClient(Database db)
        {
            return _connectionMultiplexer.GetDatabase((int) db);
        }

        public void Dispose()
        {
            _connectionMultiplexer.Dispose();
        }
    }
}