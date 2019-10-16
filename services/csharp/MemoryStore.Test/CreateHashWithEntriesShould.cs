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
    public class CreateHashWithEntriesShould
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
        public void CallHashSetAsyncOnceForAllEntries()
        {
            var entries = new Dictionary<string, string>
            {
                {"bulbasaur", "grass"},
                {"charmander", "fire"},
                {"squirtle", "water"}
            };

            var hashSetCalls = 0;
            var conditions = new List<Condition>();
            _redisTransaction
                .Setup(tx => tx.AddCondition(It.IsAny<Condition>()))
                .Callback<Condition>(condition => conditions.Add(condition))
                .Returns((ConditionResult) null);
            _redisTransaction
                .Setup(tx => tx.HashSetAsync("types", new[]
                {
                    new HashEntry("bulbasaur", "grass"),
                    new HashEntry("charmander", "fire"),
                    new HashEntry("squirtle", "water")
                }, CommandFlags.None))
                .Callback(() => hashSetCalls++)
                .Returns(Task.FromResult(true));

            _transaction.CreateHashWithEntries("types", entries);
            Assert.AreEqual(1, conditions.Count);
            Assert.AreEqual(1, hashSetCalls);
            Util.AssertConditionsAreEqual(Condition.HashLengthEqual("types", 0), conditions[0]);
        }
    }
}
