using System.Threading.Tasks;
using Improbable.OnlineServices.DataModel.Gateway;
using Improbable.OnlineServices.DataModel.Party;
using MemoryStore.Redis;
using Moq;
using NUnit.Framework;
using StackExchange.Redis;
using ITransactionRedis = StackExchange.Redis.ITransaction;

namespace MemoryStore.Test
{
    [TestFixture]
    public class EnqueueAllShould
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
        public void CallSortedSetAddAsyncForEachEntry()
        {
            var called = 0;
            _redisTransaction.Setup(tx =>
                    tx.SortedSetAddAsync("Queue::open_world", _joinRequest.Party.Id, It.IsAny<double>(), When.Always,
                        CommandFlags.PreferMaster))
                .Callback(() => called++)
                .Returns(Task.FromResult(true));
            _transaction.EnqueueAll(_joinRequest.Yield());
            Assert.AreEqual(1, called);
        }
    }
}
