namespace MemoryStore
{
    public interface IMemoryStoreClientManager<T> where T : IMemoryStoreClient
    {
        T GetClient();
    }
}