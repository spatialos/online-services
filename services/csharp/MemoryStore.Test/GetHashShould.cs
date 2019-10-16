using System.Threading.Tasks;
using MemoryStore.Redis;
using Moq;
using NUnit.Framework;
using StackExchange.Redis;

namespace MemoryStore.Test
{
    [TestFixture]
    public class GetHashShould
    {
        private static readonly string _metadataKey = "metadata";

        private static readonly HashEntry[] _metadataHash =
        {
            new HashEntry("WhatIsMyPurpose", "PassTheMetadata")
        };

        private Mock<IDatabase> _database;
        private IMemoryStoreClient _memoryStore;

        [SetUp]
        public void SetUp()
        {
            _database = new Mock<IDatabase>(MockBehavior.Strict);
            _memoryStore = new RedisClient(_database.Object, Util.CreateMockLoadedLuaScript());
        }

        [Test]
        public async Task ReturnNullIfHashNotFoundInMemoryStore()
        {
            // Setup the client such that it will claim there is no such hash with the given id.
            _database.Setup(db => db.HashGetAllAsync(_metadataKey, CommandFlags.PreferMaster)).ReturnsAsync((HashEntry[]) null);

            // Verify that null is returned.
            Assert.Null(await _memoryStore.GetHashAsync(_metadataKey));
        }

        [Test]
        public async Task ReturnHashIfFoundInMemoryStore()
        {
            // Setup the client such that it will successfully return the entry associated to the given hash. 
            _database.Setup(db => db.HashGetAllAsync(_metadataKey, CommandFlags.PreferMaster))
                .ReturnsAsync(_metadataHash);

            // Verify that the hash value was returned as response.
            var metadataResult = await _memoryStore.GetHashAsync(_metadataKey);
            CollectionAssert.AreEqual(_metadataHash.ToStringDictionary(), metadataResult);
        }
    }
}
