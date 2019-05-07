using System.Collections.Generic;
using Improbable.OnlineServices.DataModel;
using Improbable.OnlineServices.DataModel.Party;
using MemoryStore.Redis;
using Moq;
using NUnit.Framework;
using StackExchange.Redis;

namespace MemoryStore.Test
{
    [TestFixture]
    public class GetShould
    {
        private static readonly Party _party = new Party("IAmLeaderWoo", "IAmPitWoo", 10, 20,
            new Dictionary<string, string> {{"WhatIsMyPurpose", "PassTheMetadata"}});

        private static readonly string _partyKey = GetKey(_party);

        private static readonly Member _member = _party.GetLeader();
        private static readonly string _memberKey = GetKey(_member);

        private Mock<IDatabase> _database;
        private IMemoryStoreClient _memoryStore;

        [SetUp]
        public void SetUp()
        {
            _database = new Mock<IDatabase>(MockBehavior.Strict);
            _memoryStore = new RedisClient(_database.Object, Util.CreateMockLoadedLuaScript());
        }

        [Test]
        public void ReturnNullIfEntryNotFoundInMemoryStore()
        {
            // Setup the client such that it will claim there is no such party with the given id.
            _database.Setup(db => db.StringGet(_partyKey, CommandFlags.PreferMaster)).Returns(RedisValue.Null);

            // Verify that NotFoundException thrown.
            Assert.Null(_memoryStore.Get<Party>(_party.Id));
        }

        [Test]
        public void ReturnEntryIfFoundInMemoryStore()
        {
            // Setup the client such that it will successfully return the member associated to the given id. 
            _database.Setup(db => db.StringGet(_memberKey, CommandFlags.PreferMaster))
                .Returns(_member.SerializeToJson());

            // Verify that the member value was returned as response.
            var member = _memoryStore.Get<Member>(_member.Id);
            Assert.AreEqual(_member.Id, member.Id);
            Assert.AreEqual(_member.PartyId, member.PartyId);
            Assert.AreEqual(member.SerializeToJson(), member.PreviousState);
        }

        private static string GetKey(Entry entry)
        {
            return $"{entry.GetType().Name}:{entry.Id}";
        }
    }
}