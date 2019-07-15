using System.Collections.Generic;
using System.Threading.Tasks;
using Improbable.MetagameServices.DataModel;
using Improbable.MetagameServices.DataModel.Party;
using MemoryStore.Redis;
using Moq;
using NUnit.Framework;
using StackExchange.Redis;
using ITransactionRedis = StackExchange.Redis.ITransaction;

namespace MemoryStore.Test
{
    [TestFixture]
    public class UpdateAllShould
    {
        private const string TestLeaderId = "Pickle";
        private const string TestPlayerId = "BirdPerson";
        private const string DefaultPit = "DUMMY_PIT";

        private static readonly Party _party = new Party(TestLeaderId, DefaultPit, 10, 20,
            new Dictionary<string, string> { { "WhatIsMyPurpose", "PassTheMetadata" } })
        {
            MemberIdToPit = { [TestPlayerId] = DefaultPit }
        };

        private static readonly string _partyKey = GetKey(_party);

        private Mock<ITransactionRedis> _redisTransaction;
        private ITransaction _transaction;

        [SetUp]
        public void SetUp()
        {
            _party.PreviousState = _party.SerializeToJson();
            _redisTransaction = new Mock<ITransactionRedis>(MockBehavior.Strict);
            _transaction = new RedisTransaction(_redisTransaction.Object, Util.CreateMockLoadedLuaScript());
        }

        [Test]
        public void SetAppropriateConditionsAndSetStrings()
        {
            // Update some of the party's fields.
            _party.UpdatePartyLeader(TestPlayerId);

            // We should be expecting one condition, for the party we are updating. 
            var conditions = new List<Condition>();
            _redisTransaction
                .Setup(tr => tr.AddCondition(It.IsAny<Condition>()))
                .Returns((ConditionResult) null)
                .Callback<Condition>(condition => conditions.Add(condition));

            // We should be expecting one string set operation, updating the party.
            _redisTransaction.Setup(tr => tr.StringSetAsync(_partyKey, _party.SerializeToJson(), null, When.Always,
                    CommandFlags.PreferMaster))
                .Returns((Task<bool>) null);

            _transaction.UpdateAll(new List<Entry> { _party });
            _redisTransaction.Verify();

            // Existence check, unchanged check
            Assert.AreEqual(2, conditions.Count);
            var partyExistsCondition = conditions[0];
            AssertConditionsAreEqual(Condition.KeyExists(_partyKey), partyExistsCondition);
            var partyUnchangedCondition = conditions[1];
            AssertConditionsAreEqual(Condition.StringEqual(_partyKey, _party.PreviousState), partyUnchangedCondition);
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
