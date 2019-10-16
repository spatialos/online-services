using System.Collections.Generic;
using System.Threading.Tasks;
using MemoryStore.Redis;
using Moq;
using NUnit.Framework;
using StackExchange.Redis;
using ITransactionRedis = StackExchange.Redis.ITransaction;

namespace MemoryStore.Test
{
    [TestFixture]
    public class UpdateHashWithEntriesShould
    {
        private Mock<ITransactionRedis> _redisTransaction;
        private ITransaction _transaction;

        [SetUp]
        public void SetUp()
        {
            _redisTransaction = new Mock<ITransactionRedis>(MockBehavior.Strict);
            _transaction = new RedisTransaction(_redisTransaction.Object, Util.CreateMockLoadedLuaScript());
        }

        [Test]
        public void MakeCorrectRedisCalls()
        {
            var updates = new List<string>();
            _redisTransaction
                .Setup(tx => tx.HashSetAsync(
                    "my_hash", It.IsAny<RedisValue>(), It.IsAny<RedisValue>(), When.Always, CommandFlags.PreferMaster
                ))
                .Callback<RedisKey, RedisValue, RedisValue, When, CommandFlags>(
                    (hash, key, value, when, flags) => updates.Add(key)
                )
                .Returns(Task.FromResult(true));

            var deletions = new List<string>();
            _redisTransaction
                .Setup(tx => tx.HashDeleteAsync("my_hash", It.IsAny<RedisValue>(), CommandFlags.PreferMaster))
                .Callback<RedisKey, RedisValue, CommandFlags>(
                    (hash, key, flags) => deletions.Add(key)
                )
                .Returns(Task.FromResult(true));

            _transaction.UpdateHashWithEntries("my_hash", new Dictionary<string, string>
            {
                {"add", "my_value"},
                {"delete_empty", ""},
                {"delete_null", null}
            });

            Assert.AreEqual(2, deletions.Count);
            Assert.AreEqual("delete_empty", deletions[0]);
            Assert.AreEqual("delete_null", deletions[1]);

            Assert.AreEqual(1, updates.Count);
            Assert.AreEqual("add", updates[0]);
        }
    }
}
