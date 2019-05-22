using System.Collections.Generic;
using System.Threading.Tasks;
using Improbable.OnlineServices.DataModel;
using Improbable.OnlineServices.DataModel.Party;
using MemoryStore.Redis;
using Moq;
using NUnit.Framework;
using StackExchange.Redis;
using ITransactionRedis = StackExchange.Redis.ITransaction;

namespace MemoryStore.Test
{
    [TestFixture]
    public class CreateAllShould
    {
        private static readonly Party _party = new Party("IAmLeaderWoo", "IAmPitWoo", 10, 20,
            new Dictionary<string, string> { { "WhatIsMyPurpose", "PassTheMetadata" } });

        private static readonly Member _leader = new Member("IAmLeaderWoo", _party.Id);

        private static readonly string _partyKey = GetKey(_party);
        private static readonly string _leaderKey = GetKey(_leader);

        private Mock<ITransactionRedis> _redisTransaction;
        private ITransaction _transaction;

        [SetUp]
        public void SetUp()
        {
            _redisTransaction = new Mock<ITransactionRedis>(MockBehavior.Strict);
            _transaction = new RedisTransaction(_redisTransaction.Object, Util.CreateMockLoadedLuaScript());
        }

        [Test]
        public void SetAppropriateConditionsAndStrings()
        {
            // We should be expecting two conditions: one per each entry.
            var conditions = new List<Condition>();
            _redisTransaction
                .Setup(tr => tr.AddCondition(It.IsAny<Condition>()))
                .Returns((ConditionResult)null)
                .Callback<Condition>(condition => conditions.Add(condition));

            // We should be expecting two string set calls: one for the party entry and another for the member entry.
            _redisTransaction
                .Setup(tr =>
                    tr.StringSetAsync(_partyKey, _party.SerializeToJson(), null, When.Always,
                        CommandFlags.PreferMaster))
                .Returns((Task<bool>)null)
                .Verifiable();
            _redisTransaction
                .Setup(tr =>
                    tr.StringSetAsync(_leaderKey, _leader.SerializeToJson(), null, When.Always,
                        CommandFlags.PreferMaster))
                .Returns((Task<bool>)null)
                .Verifiable();

            _transaction.CreateAll(new List<Entry> { _party, _leader });
            _redisTransaction.Verify();

            Assert.AreEqual(2, conditions.Count);
            var partyCondition = conditions[0];
            AssertConditionsAreEqual(Condition.KeyNotExists(_partyKey), partyCondition);

            var leaderCondition = conditions[1];
            AssertConditionsAreEqual(Condition.KeyNotExists(_leaderKey), leaderCondition);
        }

        // Conditions do not have Equals overriden. Performing #Equals(...) will return true iff they are the same 
        // object. By comparing their string representations, we reveal the actual contents of the condition and can
        // verify whether they impose the same condition or not.
        private static void AssertConditionsAreEqual(Condition expected, Condition received)
        {
            Assert.AreEqual(expected.ToString(), received.ToString());
        }

        private static string GetKey(Entry entry)
        {
            return $"{entry.GetType().Name}:{entry.Id}";
        }
    }
}