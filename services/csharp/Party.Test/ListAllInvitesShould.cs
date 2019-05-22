using Grpc.Core;
using Improbable.OnlineServices.DataModel.Party;
using Improbable.OnlineServices.Proto.Invite;
using MemoryStore;
using Moq;
using NUnit.Framework;
using InviteDataModel = Improbable.OnlineServices.DataModel.Party.Invite;
using InviteProto = Improbable.OnlineServices.Proto.Invite.Invite;

namespace Party.Test
{
    [TestFixture]
    public class ListAllInvitesShould
    {
        private const string PlayerId1 = "sending";
        private const string PlayerId2 = "sending and receiving";
        private const string PlayerId3 = "receiving";
        private const string PartyId1 = "party1";
        private const string PartyId2 = "party2";

        private InviteDataModel _storedOutboundInvite;
        private InviteDataModel _storedInboundInvite;
        private PlayerInvites _storedPlayerInvites;
        private Mock<ITransaction> _mockTransaction;
        private Mock<IMemoryStoreClient> _mockMemoryStoreClient;
        private InviteServiceImpl _inviteService;

        [SetUp]
        public void SetUp()
        {
            _storedOutboundInvite = new InviteDataModel(PlayerId1, PlayerId2, PartyId1);
            _storedInboundInvite = new InviteDataModel(PlayerId2, PlayerId3, PartyId2);
            _storedPlayerInvites = new PlayerInvites(PlayerId2);
            _storedPlayerInvites.InboundInviteIds.Add(_storedInboundInvite.Id);
            _storedPlayerInvites.OutboundInviteIds.Add(_storedOutboundInvite.Id);

            _mockTransaction = new Mock<ITransaction>(MockBehavior.Strict);
            _mockMemoryStoreClient = new Mock<IMemoryStoreClient>(MockBehavior.Strict);
            _mockMemoryStoreClient.Setup(client => client.CreateTransaction()).Returns(_mockTransaction.Object);
            _mockMemoryStoreClient.Setup(client => client.Dispose()).Verifiable();

            var memoryStoreClientManager = new Mock<IMemoryStoreClientManager<IMemoryStoreClient>>(MockBehavior.Strict);
            memoryStoreClientManager.Setup(manager => manager.GetClient()).Returns(_mockMemoryStoreClient.Object);
            _inviteService = new InviteServiceImpl(memoryStoreClientManager.Object);
        }

        [Test]
        public void ReturnEarlyIfNoPlayerInvitesExistsForTheCaller()
        {
            // Setup the client such that it will claim there is no invites for the requested player.
            _mockMemoryStoreClient.Setup(client => client.Get<PlayerInvites>(PlayerId2)).Returns((PlayerInvites)null);

            // Verify that the response is empty.
            var context = Util.CreateFakeCallContext(PlayerId2, "");
            Assert.AreEqual(new ListAllInvitesResponse(),
                _inviteService.ListAllInvites(new ListAllInvitesRequest(), context).Result);
        }

        [Test]
        public void ReturnUnavailableWhenAnInviteDoesNotExist()
        {
            // Setup the client such that it will claim there is no such invite with the given id.
            _mockMemoryStoreClient.Setup(client => client.Get<PlayerInvites>(PlayerId2)).Returns(_storedPlayerInvites);
            _mockMemoryStoreClient.Setup(client => client.Get<InviteDataModel>(_storedOutboundInvite.Id))
                .Returns((InviteDataModel)null);

            // Verify that the request has thrown an exception.
            var context = Util.CreateFakeCallContext(PlayerId2, "");
            var exception =
                Assert.Throws<RpcException>(() => _inviteService.ListAllInvites(new ListAllInvitesRequest(), context));
            Assert.AreEqual(StatusCode.Unavailable, exception.StatusCode);
        }

        [Test]
        public void ReturnPlayerInvitesIfTheyExist()
        {
            // Setup the client such that it will claim the user making this request is not involved in the invite.
            _mockMemoryStoreClient.Setup(client => client.Get<PlayerInvites>(PlayerId2)).Returns(_storedPlayerInvites);
            _mockMemoryStoreClient.Setup(client => client.Get<InviteDataModel>(_storedOutboundInvite.Id))
                .Returns(_storedOutboundInvite);
            _mockMemoryStoreClient.Setup(client => client.Get<InviteDataModel>(_storedInboundInvite.Id))
                .Returns(_storedInboundInvite);

            // Check that the expected invites have been returned.
            var context = Util.CreateFakeCallContext(PlayerId2, "");
            var playerInvites = _inviteService.ListAllInvites(new ListAllInvitesRequest(), context).Result;

            Assert.AreEqual(1, playerInvites.InboundInvites.Count);
            var inboundInvite = playerInvites.InboundInvites[0];
            InviteComparator.AssertEquivalent(_storedInboundInvite, inboundInvite);

            Assert.AreEqual(1, playerInvites.OutboundInvites.Count);
            var outboundInvite = playerInvites.OutboundInvites[0];
            InviteComparator.AssertEquivalent(_storedOutboundInvite, outboundInvite);
        }
    }
}