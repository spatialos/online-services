using System.Collections.Generic;
using Grpc.Core;
using Improbable.OnlineServices.Common.Analytics;
using Improbable.OnlineServices.DataModel;
using Improbable.OnlineServices.DataModel.Party;
using Improbable.OnlineServices.Proto.Invite;
using MemoryStore;
using Moq;
using NUnit.Framework;
using InviteDataModel = Improbable.OnlineServices.DataModel.Party.Invite;

namespace Party.Test
{
    public class DeleteInviteShould
    {
        private const string SenderPlayerId = "Sender";
        private const string ReceiverPlayerId = "Receiver";
        private const string PartyId = "Party";
        private const string AnalyticsEventType = "player_invite_to_party_revoked";

        private static InviteDataModel _invite;
        private Mock<ITransaction> _mockTransaction;
        private Mock<IMemoryStoreClient> _mockMemoryStoreClient;
        private Mock<IAnalyticsSender> _mockAnalyticsSender;
        private InviteServiceImpl _inviteService;

        [SetUp]
        public void SetUp()
        {
            _invite = new InviteDataModel(SenderPlayerId, ReceiverPlayerId, PartyId);

            _mockTransaction = new Mock<ITransaction>(MockBehavior.Strict);
            _mockMemoryStoreClient = new Mock<IMemoryStoreClient>(MockBehavior.Strict);
            _mockMemoryStoreClient.Setup(client => client.CreateTransaction()).Returns(_mockTransaction.Object);
            _mockMemoryStoreClient.Setup(client => client.Dispose()).Verifiable();
            _mockAnalyticsSender = new Mock<IAnalyticsSender>(MockBehavior.Strict);

            var memoryStoreClientManager = new Mock<IMemoryStoreClientManager<IMemoryStoreClient>>(MockBehavior.Strict);
            memoryStoreClientManager.Setup(manager => manager.GetClient()).Returns(_mockMemoryStoreClient.Object);
            _inviteService = new InviteServiceImpl(memoryStoreClientManager.Object, _mockAnalyticsSender.Object);
        }

        [Test]
        public void ReturnInvalidArgumentWhenNoInviteIdIsProvided()
        {
            // Check that having a non-empty invite id is enforced by DeleteInvite.
            var context = Util.CreateFakeCallContext(SenderPlayerId, "");
            var exception =
                Assert.ThrowsAsync<RpcException>(() => _inviteService.DeleteInvite(new DeleteInviteRequest(), context));
            Assert.That(exception.Message, Contains.Substring("non-empty invite"));
            Assert.AreEqual(StatusCode.InvalidArgument, exception.StatusCode);
        }

        [Test]
        public void ReturnPermissionsDeniedWhenThePlayerIsNotInvolvedInTheInvite()
        {
            // Setup the client such that it will claim the user making this request is not involved in the invite.
            _mockMemoryStoreClient.Setup(client => client.GetAsync<InviteDataModel>(_invite.Id)).ReturnsAsync(_invite);

            // Check that player involvement is enforced by DeleteInvite.
            var context = Util.CreateFakeCallContext("SomeoneElse", "");
            var request = new DeleteInviteRequest { InviteId = _invite.Id };
            var exception = Assert.ThrowsAsync<RpcException>(() => _inviteService.DeleteInvite(request, context));
            Assert.That(exception.Message, Contains.Substring("not involved"));
            Assert.AreEqual(StatusCode.PermissionDenied, exception.StatusCode);
        }

        [Test]
        public void ReturnEarlyWhenThereIsNoSuchInvite()
        {
            // Setup the client such that it will claim there is no such invite with the given id.
            _mockMemoryStoreClient.Setup(client => client.GetAsync<InviteDataModel>(_invite.Id))
                .ReturnsAsync((InviteDataModel) null);

            // Verify that the request has finished without any errors being thrown. 
            var context = Util.CreateFakeCallContext(SenderPlayerId, "");
            var request = new DeleteInviteRequest { InviteId = _invite.Id };
            Assert.AreEqual(new DeleteInviteResponse(), _inviteService.DeleteInvite(request, context).Result);
        }

        [Test]
        public void ReturnEntryNotFoundIfSenderPlayerInvitesNotFound()
        {
            // Setup the client such that it will claim there are no PlayerInvites for the sender.
            _mockMemoryStoreClient.Setup(client => client.GetAsync<InviteDataModel>(_invite.Id)).ReturnsAsync(_invite);
            _mockMemoryStoreClient.Setup(client => client.GetAsync<PlayerInvites>(SenderPlayerId))
                .ReturnsAsync((PlayerInvites) null);

            // Check that an EntryNotFoundException is thrown as a result.
            var context = Util.CreateFakeCallContext(SenderPlayerId, "");
            var request = new DeleteInviteRequest { InviteId = _invite.Id };
            var exception =
                Assert.ThrowsAsync<EntryNotFoundException>(() => _inviteService.DeleteInvite(request, context));
            Assert.AreEqual("No invites found for the sender", exception.Message);
        }

        [Test]
        public void ReturnEntryNotFoundIfReceiverPlayerInvitesNotFound()
        {
            // Setup the client such that it will claim there are no PlayerInvites for the receiver.
            _mockMemoryStoreClient.Setup(client => client.GetAsync<InviteDataModel>(_invite.Id)).ReturnsAsync(_invite);
            _mockMemoryStoreClient.Setup(client => client.GetAsync<PlayerInvites>(SenderPlayerId))
                .ReturnsAsync(new PlayerInvites(SenderPlayerId));
            _mockMemoryStoreClient.Setup(client => client.GetAsync<PlayerInvites>(ReceiverPlayerId))
                .ReturnsAsync((PlayerInvites) null);

            // Check that an EntryNotFoundException is thrown as a result.
            var context = Util.CreateFakeCallContext(SenderPlayerId, "");
            var request = new DeleteInviteRequest { InviteId = _invite.Id };
            var exception =
                Assert.ThrowsAsync<EntryNotFoundException>(() => _inviteService.DeleteInvite(request, context));
            Assert.AreEqual("No invites found for the receiver", exception.Message);
        }

        [Test]
        public void ReturnEmptyResponseWhenSuccessfullyDeletingInvite()
        {
            // Setup the client such that it will successfully delete the invite.
            var senderInvites = new PlayerInvites(SenderPlayerId);
            senderInvites.OutboundInviteIds.Add(_invite.Id);
            var receiverInvites = new PlayerInvites(ReceiverPlayerId);
            receiverInvites.InboundInviteIds.Add(_invite.Id);

            var entriesToDelete = new List<Entry>();
            var entriesToUpdate = new List<Entry>();
            _mockMemoryStoreClient.Setup(client => client.GetAsync<InviteDataModel>(_invite.Id)).ReturnsAsync(_invite);
            _mockMemoryStoreClient.Setup(client => client.GetAsync<PlayerInvites>(SenderPlayerId))
                .ReturnsAsync(senderInvites);
            _mockMemoryStoreClient.Setup(client => client.GetAsync<PlayerInvites>(ReceiverPlayerId))
                .ReturnsAsync(receiverInvites);
            _mockTransaction.Setup(tr => tr.DeleteAll(It.IsAny<IEnumerable<Entry>>()))
                .Callback<IEnumerable<Entry>>(entries => entriesToDelete.AddRange(entries));
            _mockTransaction.Setup(tr => tr.UpdateAll(It.IsAny<IEnumerable<Entry>>()))
                .Callback<IEnumerable<Entry>>(entries => entriesToUpdate.AddRange(entries));
            _mockTransaction.Setup(tr => tr.Dispose());

            _mockAnalyticsSender.Setup(
                sender =>
                    sender.Send(AnalyticsConstants.InviteClass, AnalyticsEventType,
                                new Dictionary<string, string>
                                {
                                    { AnalyticsConstants.PlayerId, ReceiverPlayerId },
                                    { AnalyticsConstants.PartyId, PartyId },
                                    { AnalyticsConstants.PlayerIdInviter, SenderPlayerId },
                                    { AnalyticsConstants.InviteId, _invite.Id }
                                }, ReceiverPlayerId));

            // Verify that an empty response was returned.
            var context = Util.CreateFakeCallContext(SenderPlayerId, "");
            var request = new DeleteInviteRequest { InviteId = _invite.Id };
            Assert.AreEqual(new DeleteInviteResponse(), _inviteService.DeleteInvite(request, context).Result);

            // Verify that the invite was deleted.
            Assert.AreEqual(1, entriesToDelete.Count);
            Assert.IsInstanceOf<InviteDataModel>(entriesToDelete[0]);
            var inviteDeleted = (InviteDataModel) entriesToDelete[0];
            Assert.AreEqual(_invite.Id, inviteDeleted.Id);
            Assert.AreEqual(_invite.SenderId, inviteDeleted.SenderId);
            Assert.AreEqual(_invite.ReceiverId, inviteDeleted.ReceiverId);
            Assert.AreEqual(_invite.PartyId, inviteDeleted.PartyId);

            // Verify that the sender's and receiver's player invites have been updated accordingly.
            Assert.AreEqual(2, entriesToUpdate.Count);
            Assert.IsInstanceOf<PlayerInvites>(entriesToUpdate[0]);
            Assert.IsInstanceOf<PlayerInvites>(entriesToUpdate[1]);

            var updatedSenderInvites = (PlayerInvites) entriesToUpdate[0];
            Assert.AreEqual(SenderPlayerId, updatedSenderInvites.Id);
            Assert.That(updatedSenderInvites.OutboundInviteIds, Does.Not.Contain(_invite.Id));

            var updatedReceiverInvites = (PlayerInvites) entriesToUpdate[1];
            Assert.AreEqual(ReceiverPlayerId, updatedReceiverInvites.Id);
            Assert.That(updatedReceiverInvites.InboundInviteIds, Does.Not.Contain(_invite.Id));

            _mockAnalyticsSender.VerifyAll();
        }
    }
}
