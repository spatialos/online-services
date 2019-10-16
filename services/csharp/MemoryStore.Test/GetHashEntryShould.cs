using System.Threading.Tasks;
using MemoryStore.Redis;
using Moq;
using NUnit.Framework;
using StackExchange.Redis;

namespace MemoryStore.Test
{
    [TestFixture]
    public class GetHashEntryShould
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
        public async Task ReturnNullIfHashEntryNotFoundInMemoryStore()
        {
            // Setup the client such that it will claim there is no such hash entry with the given id + key.
            _database.Setup(db => db.HashGetAsync(_metadataKey, _metadataHash[0].Name, CommandFlags.PreferMaster))
                .ReturnsAsync(RedisValue.Null);

            // Verify that null is returned.
            Assert.Null(await _memoryStore.GetHashEntryAsync(_metadataKey, _metadataHash[0].Name));
        }

        [Test]
        public async Task ReturnHashEntryIfFoundInMemoryStore()
        {
            // Setup the client such that it will successfully return the hash value associated to the given hash. 
            _database.Setup(db => db.HashGetAsync(_metadataKey, _metadataHash[0].Name, CommandFlags.PreferMaster))
                .ReturnsAsync(_metadataHash[0].Value);

            // Verify that the hash value was returned as response.
            var metadataEntryResult = await _memoryStore.GetHashEntryAsync(_metadataKey, _metadataHash[0].Name);

            Assert.AreEqual(metadataEntryResult, _metadataHash[0].Value.ToString());
        }
    }
}
