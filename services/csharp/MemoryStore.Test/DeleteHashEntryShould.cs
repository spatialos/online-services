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
    public class DeleteHashEntryShould
    {
        private const string MetadataKey = "metadata";

        private Mock<ITransactionRedis> _redisTransaction;
        private ITransaction _transaction;

        [SetUp]
        public void SetUp()
        {
            _redisTransaction = new Mock<ITransactionRedis>(MockBehavior.Strict);
            _transaction = new RedisTransaction(_redisTransaction.Object, Util.CreateMockLoadedLuaScript());
        }

        [Test]
        public void SetAppropriateConditionsAndDeleteKey()
        {
            // We should be expecting two conditions - one for checking that the hash is present, and
            // one for the specified key in that hash.
            var conditions = new List<Condition>();
            _redisTransaction
                .Setup(tr => tr.AddCondition(It.IsAny<Condition>()))
                .Returns((ConditionResult) null)
                .Callback<Condition>(condition => conditions.Add(condition));

            _redisTransaction.Setup(tr => tr.HashDeleteAsync(MetadataKey, "WhatIsMyPurpose", CommandFlags.PreferMaster))
                .Returns((Task<bool>) null);

            _transaction.DeleteHashEntry(MetadataKey, "WhatIsMyPurpose");
            _redisTransaction.Verify();

            Assert.AreEqual(2, conditions.Count);

            var hashExistsCondition = conditions[0];
            AssertConditionsAreEqual(Condition.KeyExists(MetadataKey), hashExistsCondition);
            var keyExistsCondition = conditions[1];
            AssertConditionsAreEqual(Condition.HashExists(MetadataKey, "WhatIsMyPurpose"), keyExistsCondition);
        }

        private static void AssertConditionsAreEqual(Condition expected, Condition received)
        {
            Assert.AreEqual(expected.ToString(), received.ToString());
        }
    }
}
