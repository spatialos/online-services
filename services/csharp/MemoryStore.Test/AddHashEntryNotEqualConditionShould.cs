using System.Collections.Generic;
using MemoryStore.Redis;
using Moq;
using NUnit.Framework;
using StackExchange.Redis;
using ITransactionRedis = StackExchange.Redis.ITransaction;

namespace MemoryStore.Test
{
    [TestFixture]
    public class AddHashEntryNotEqualConditionShould
    {
        private Mock<ITransactionRedis> _redisTransaction;
        private ITransaction _transaction;

        [SetUp]
        public void SetUp()
        {
            _redisTransaction = new Mock<ITransactionRedis>();
            _transaction = new RedisTransaction(_redisTransaction.Object, Util.CreateMockLoadedLuaScript());
        }

        [Test]
        public void SetCorrectConditionOnInternalTransaction()
        {
            var conditions = new List<Condition>();
            _redisTransaction.Setup(tx => tx.AddCondition(It.IsAny<Condition>()))
                .Callback<Condition>(condition => conditions.Add(condition))
                .Returns((ConditionResult) null);

            _transaction.AddHashEntryNotEqualCondition("my-hash", "my-key", "my-value");

            Assert.AreEqual(1, conditions.Count);
            Util.AssertConditionsAreEqual(Condition.HashNotEqual("my-hash", "my-key", "my-value"), conditions[0]);
        }
    }
}
