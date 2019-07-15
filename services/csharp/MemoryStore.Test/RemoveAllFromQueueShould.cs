using System.Threading.Tasks;
using Improbable.MetagameServices.DataModel.Gateway;
using Improbable.MetagameServices.DataModel.Party;
using MemoryStore.Redis;
using Moq;
using NUnit.Framework;
using StackExchange.Redis;
using ITransactionRedis = StackExchange.Redis.ITransaction;

namespace MemoryStore.Test
{
    [TestFixture]
    public class RemoveAllFromQueueShould
    {
        private readonly PartyJoinRequest _joinRequest =
            new PartyJoinRequest(new Party("Leader", "PIT"), "open_world", null);

        private Mock<ITransactionRedis> _redisTransaction;
        private ITransaction _transaction;

        [SetUp]
        public void SetUp()
        {
            _redisTransaction = new Mock<ITransactionRedis>(MockBehavior.Strict);
            _transaction = new RedisTransaction(_redisTransaction.Object, Util.CreateMockLoadedLuaScript());
        }

        [Test]
        public void CallSortedSetRemoveAsyncForEachEntry()
        {
            var called = 0;
            _redisTransaction.Setup(tx =>
                    tx.SortedSetRemoveAsync("Queue::open_world", _joinRequest.Party.Id, CommandFlags.PreferMaster))
                .Callback(() => called++)
                .Returns(Task.FromResult(true));
            _transaction.RemoveAllFromQueue(_joinRequest.Yield());
            Assert.AreEqual(1, called);
        }
    }
}
