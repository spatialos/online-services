namespace MemoryStore.Redis
{
    public interface IRedisClient : IMemoryStoreClient
    {
        IRedisTransaction CreateRedisTransaction();
    }
}
