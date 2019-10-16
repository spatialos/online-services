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
    public class DeleteKeyShould
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
        public void SetAppropriateConditionAndDeleteKey()
        {
            var conditions = new List<Condition>();
            _redisTransaction
                .Setup(tr => tr.AddCondition(It.IsAny<Condition>()))
                .Returns((ConditionResult) null)
                .Callback<Condition>(condition => conditions.Add(condition));

            _redisTransaction.Setup(tr => tr.KeyDeleteAsync(MetadataKey, CommandFlags.PreferMaster))
                .Returns((Task<bool>) null);

            _transaction.DeleteKey(MetadataKey);
            _redisTransaction.Verify();

            Assert.AreEqual(1, conditions.Count);

            var keyExistsCondition = conditions[0];
            AssertConditionsAreEqual(Condition.KeyExists(MetadataKey), keyExistsCondition);
        }

        private static void AssertConditionsAreEqual(Condition expected, Condition received)
        {
            Assert.AreEqual(expected.ToString(), received.ToString());
        }
    }
}
