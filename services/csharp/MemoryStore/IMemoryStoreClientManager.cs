namespace MemoryStore
{
    public interface IMemoryStoreClientManager<out T> where T : IMemoryStoreClient
    {
        T GetClient();
    }
}
