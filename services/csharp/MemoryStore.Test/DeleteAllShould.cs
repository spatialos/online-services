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
    public class DeleteAllShould
    {
        private const string TestLeaderId = "Lupin";
        private const string TestPlayerId = "Fawkes";
        private const string DefaultPit = "DUMMY_PIT";

        private static readonly Party _party = new Party(TestLeaderId, DefaultPit, 10, 20,
            new Dictionary<string, string> {{"WhatIsMyPurpose", "PassTheMetadata"}})
        {
            MemberIdToPit = {[TestPlayerId] = DefaultPit}
        };

        private static readonly string _partyKey = GetKey(_party);

        private static readonly Member _leader = _party.GetLeader();
        private static readonly string _leaderKey = GetKey(_leader);

        private static readonly Member _member = _party.GetMember(TestPlayerId);
        private static readonly string _memberKey = GetKey(_member);

        private Mock<ITransactionRedis> _redisTransaction;
        private ITransaction _transaction;

        [SetUp]
        public void SetUp()
        {
            _party.PreviousState = _party.SerializeToJson();
            _member.PreviousState = _member.SerializeToJson();
            _redisTransaction = new Mock<ITransactionRedis>(MockBehavior.Strict);
            _transaction = new RedisTransaction(_redisTransaction.Object, Util.CreateMockLoadedLuaScript());
        }

        [Test]
        public void SetAppropriateConditionsAndDeleteKeys()
        {
            // We should be expecting six conditions: one for the party and two for its two members, plus
            // three corresponding existence checks.
            var conditions = new List<Condition>();
            _redisTransaction
                .Setup(tr => tr.AddCondition(It.IsAny<Condition>()))
                .Returns((ConditionResult) null)
                .Callback<Condition>(condition => conditions.Add(condition));

            // We should be expecting three delete operations: one for the party and two for its members.
            _redisTransaction.Setup(tr => tr.KeyDeleteAsync(_partyKey, CommandFlags.PreferMaster))
                .Returns((Task<bool>) null);
            _redisTransaction.Setup(tr => tr.KeyDeleteAsync(_leaderKey, CommandFlags.PreferMaster))
                .Returns((Task<bool>) null);
            _redisTransaction.Setup(tr => tr.KeyDeleteAsync(_memberKey, CommandFlags.PreferMaster))
                .Returns((Task<bool>) null);

            _transaction.DeleteAll(new List<Entry> {_party, _leader, _member});
            _redisTransaction.Verify();

            Assert.AreEqual(6, conditions.Count);

            var partyExistsCondition = conditions[0];
            AssertConditionsAreEqual(Condition.KeyExists(_partyKey), partyExistsCondition);

            var partyUnchangedCondition = conditions[1];
            AssertConditionsAreEqual(Condition.StringEqual(_partyKey, _party.SerializeToJson()),
                partyUnchangedCondition);

            var leaderExistsCondition = conditions[2];
            AssertConditionsAreEqual(Condition.KeyExists(_leaderKey), leaderExistsCondition);

            var leaderUnchangedCondition = conditions[3];
            AssertConditionsAreEqual(Condition.StringEqual(_leaderKey, _leader.SerializeToJson()),
                leaderUnchangedCondition);

            var memberExistsCondition = conditions[4];
            AssertConditionsAreEqual(Condition.KeyExists(_memberKey), memberExistsCondition);

            var memberUnchangedCondition = conditions[5];
            AssertConditionsAreEqual(Condition.StringEqual(_memberKey, _member.SerializeToJson()),
                memberUnchangedCondition);
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