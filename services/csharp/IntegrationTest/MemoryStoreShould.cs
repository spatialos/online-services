using System.Collections.Generic;
using System.Threading.Tasks;
using Improbable.OnlineServices.DataModel;
using MemoryStore;
using MemoryStore.Redis;
using NUnit.Framework;

namespace IntegrationTest
{
    public class MemoryStoreShould
    {
        private const string RedisConnection = "localhost:6379";

        private RedisClientManager _memoryStoreManager;

        private class FakeQueuedEntry : QueuedEntry
        {
            internal FakeQueuedEntry(string id, string queueName)
            {
                Id = id;
                QueueName = queueName;
                Score = 1;
            }
        }

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _memoryStoreManager = new RedisClientManager(RedisConnection);
        }

        [TearDown]
        public void TearDown()
        {
            using (var memoryStoreManager = new RedisClientManager(RedisConnection))
            {
                var client = memoryStoreManager.GetRawClient(Database.Default);
                client.Execute("flushdb");
            }
        }

        [Test]
        public void ThrowInsufficientEntriesExceptionWhenDequeuingMoreEntriesThanAvailable()
        {
            var queuedEntries = new QueuedEntry[]
            {
                new FakeQueuedEntry("random1", "randomQueue"),
                new FakeQueuedEntry("random2", "randomQueue"),
                new FakeQueuedEntry("random3", "randomQueue")
            };

            var memoryStoreClient = _memoryStoreManager.GetClient();

            using (var tx = memoryStoreClient.CreateTransaction())
            {
                tx.EnqueueAll(queuedEntries);
            }

            Assert.ThrowsAsync<InsufficientEntriesException>(() =>
            {
                using (var tx = memoryStoreClient.CreateTransaction())
                {
                    return tx.DequeueAsync("randomQueue", 4);
                }
            });
        }

        [Test]
        public void ThrowEntryAlreadyExistsExceptionWhenHashEntryAlreadyExists()
        {
            var memoryStoreClient = _memoryStoreManager.GetClient();

            using (var tx = memoryStoreClient.CreateTransaction())
            {
                tx.CreateHashWithEntries("WasHereFirst", new[]
                {
                    new KeyValuePair<string, string>("Never", "Going"),
                    new KeyValuePair<string, string>("To", "Give")
                });
            }

            Assert.ThrowsAsync<EntryAlreadyExistsException>(() =>
            {
                using (var tx = memoryStoreClient.CreateTransaction())
                {
                    tx.CreateHashWithEntries("WasHereFirst", new[]
                    {
                        new KeyValuePair<string, string>("You", "Up")
                    });
                }

                return Task.CompletedTask;
            });
        }

        [Test]
        public void ThrowEntryNotFoundExceptionWhenDeletingMissingKey()
        {
            var memoryStoreClient = _memoryStoreManager.GetClient();

            Assert.ThrowsAsync<EntryNotFoundException>(() =>
            {
                using (var tx = memoryStoreClient.CreateTransaction())
                {
                    tx.DeleteKey("MissingKey");
                }

                return Task.CompletedTask;
            });
        }


        [Test]
        public void ThrowFailedConditionExceptionWhenComparingHashEntryEquality()
        {
            var memoryStoreClient = _memoryStoreManager.GetClient();

            using (var tx = memoryStoreClient.CreateTransaction())
            {
                tx.CreateHashWithEntries("WasHereFirst", new[]
                {
                    new KeyValuePair<string, string>("Never", "Going"),
                    new KeyValuePair<string, string>("To", "Give")
                });
            }

            Assert.ThrowsAsync<FailedConditionException>(() =>
            {
                using (var tx = memoryStoreClient.CreateTransaction())
                {
                    tx.AddHashEntryEqualCondition("WasHereFirst", "To", "Let");
                    tx.UpdateHashWithEntries("WasHereFirst", new[]
                    {
                        new KeyValuePair<string, string>("You", "Up")
                    });
                }

                return Task.CompletedTask;
            });
        }
    }
}
