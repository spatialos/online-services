using System;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Improbable.MetagameServices.Proto.Invite;
using Improbable.MetagameServices.Proto.Party;
using Improbable.SpatialOS.Platform.Common;
using Improbable.SpatialOS.PlayerAuth.V2Alpha1;
using MemoryStore.Redis;
using NUnit.Framework;

namespace IntegrationTest
{
    public class InviteSystemShould
    {
        private const string RedisConnection = "127.0.0.1:6379";
        private const string PartyServerTarget = "127.0.0.1:4041";
        private const string TestProvider = "test_provider";
        private const string PitRequestHeaderName = "player-identity-token";

        private const string LeaderPlayerId = "Leader";
        private const string PlayerId1 = "AnotherPlayer";
        private const string PlayerId2 = "TotallySomeoneElse";

        private static string _projectName;
        private static PlayerAuthServiceClient _authServiceClient;
        private static PartyService.PartyServiceClient _partyClient;
        private static InviteService.InviteServiceClient _inviteClient;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _projectName = Environment.GetEnvironmentVariable("SPATIAL_PROJECT");
            if (string.IsNullOrEmpty(_projectName))
            {
                Assert.Fail("Project name is missing from environment.");
            }

            var refreshToken = Environment.GetEnvironmentVariable("SPATIAL_REFRESH_TOKEN");
            if (string.IsNullOrEmpty(refreshToken))
            {
                Assert.Fail("Refresh token is missing from environment."); 
            }
            _authServiceClient =
                PlayerAuthServiceClient.Create(credentials: new PlatformRefreshTokenCredential(refreshToken));
            var channel = new Channel(PartyServerTarget, ChannelCredentials.Insecure);
            _partyClient = new PartyService.PartyServiceClient(channel);
            _inviteClient = new InviteService.InviteServiceClient(channel);
        }

        [TearDown]
        public void TearDown()
        {
            using (var memoryStoreManager = new RedisClientManager(RedisConnection))
            {
                var client = memoryStoreManager.GetRawClient(Database.DEFAULT);
                client.Execute("flushdb");
            }
        }

        [Test]
        public void ReturnPermissionDeniedErrorIfPitNotProvided()
        {
            var exception = Assert.Throws<RpcException>(() => _inviteClient.CreateInvite(new CreateInviteRequest()));
            Assert.AreEqual(StatusCode.PermissionDenied, exception.StatusCode);
        }

        [Test]
        public void AllowPlayersToSendInvites()
        {
            // Create a party.
            var senderPit = CreatePlayerIdentityTokenForPlayer(LeaderPlayerId);
            var partyId = _partyClient
                .CreateParty(new CreatePartyRequest(), new Metadata { { PitRequestHeaderName, senderPit } }).PartyId;

            // Send invites to other players.
            var createInviteRequest = new CreateInviteRequest { ReceiverPlayerId = PlayerId1 };
            var inviteId1 = _inviteClient
                .CreateInvite(createInviteRequest, new Metadata { { PitRequestHeaderName, senderPit } })
                .InviteId;
            Assert.NotNull(inviteId1);

            createInviteRequest = new CreateInviteRequest { ReceiverPlayerId = PlayerId2 };
            var inviteId2 = _inviteClient
                .CreateInvite(createInviteRequest, new Metadata { { PitRequestHeaderName, senderPit } })
                .InviteId;
            Assert.NotNull(inviteId2);

            // Verify that the invites were successfully stored.
            var invite1 = _inviteClient.GetInvite(new GetInviteRequest { InviteId = inviteId1 },
                new Metadata { { PitRequestHeaderName, senderPit } }).Invite;
            Assert.AreEqual(inviteId1, invite1.Id);
            Assert.AreEqual(LeaderPlayerId, invite1.SenderPlayerId);
            Assert.AreEqual(PlayerId1, invite1.ReceiverPlayerId);
            Assert.AreEqual(partyId, invite1.PartyId);
            Assert.AreEqual(Invite.Types.Status.Pending, invite1.CurrentStatus);

            var invite2 = _inviteClient.GetInvite(new GetInviteRequest { InviteId = inviteId2 },
                new Metadata { { PitRequestHeaderName, senderPit } }).Invite;
            Assert.AreEqual(inviteId2, invite2.Id);
            Assert.AreEqual(LeaderPlayerId, invite2.SenderPlayerId);
            Assert.AreEqual(PlayerId2, invite2.ReceiverPlayerId);
            Assert.AreEqual(partyId, invite2.PartyId);
            Assert.AreEqual(Invite.Types.Status.Pending, invite2.CurrentStatus);

            // Verify that the sender has the invites in its outbound invites.
            var senderPlayerInvites = _inviteClient.ListAllInvites(new ListAllInvitesRequest(),
                new Metadata { { PitRequestHeaderName, senderPit } });
            Assert.AreEqual(2, senderPlayerInvites.OutboundInvites.Count);
            Assert.AreEqual(invite1, senderPlayerInvites.OutboundInvites[0]);
            Assert.AreEqual(invite2, senderPlayerInvites.OutboundInvites[1]);

            // Verify that both receivers have the invite in their inbound invites.
            var receiverPit1 = CreatePlayerIdentityTokenForPlayer(PlayerId1);
            var receiverPlayerInvites1 = _inviteClient.ListAllInvites(new ListAllInvitesRequest(),
                new Metadata { { PitRequestHeaderName, receiverPit1 } });
            Assert.AreEqual(1, receiverPlayerInvites1.InboundInvites.Count);
            Assert.AreEqual(invite1, receiverPlayerInvites1.InboundInvites[0]);

            var receiverPit2 = CreatePlayerIdentityTokenForPlayer(PlayerId2);
            var receiverPlayerInvites2 = _inviteClient.ListAllInvites(new ListAllInvitesRequest(),
                new Metadata { { PitRequestHeaderName, receiverPit2 } });
            Assert.AreEqual(1, receiverPlayerInvites2.InboundInvites.Count);
            Assert.AreEqual(invite2, receiverPlayerInvites2.InboundInvites[0]);

            // Cleanup.
            _inviteClient.DeleteInvite(new DeleteInviteRequest { InviteId = inviteId1 },
                new Metadata { { PitRequestHeaderName, senderPit } });
            _inviteClient.DeleteInvite(new DeleteInviteRequest { InviteId = inviteId2 },
                new Metadata { { PitRequestHeaderName, senderPit } });
            _partyClient.DeleteParty(new DeletePartyRequest(), new Metadata { { PitRequestHeaderName, senderPit } });
        }

        [Test]
        public void AllowThoseInvolvedInInvitesToDeleteThem()
        {
            // Create a party.
            var senderPit = CreatePlayerIdentityTokenForPlayer(LeaderPlayerId);
            _partyClient.CreateParty(new CreatePartyRequest(), new Metadata { { PitRequestHeaderName, senderPit } });

            // Send invites to other players.
            var createInviteRequest = new CreateInviteRequest { ReceiverPlayerId = PlayerId1 };
            var inviteId1 = _inviteClient
                .CreateInvite(createInviteRequest, new Metadata { { PitRequestHeaderName, senderPit } })
                .InviteId;
            Assert.NotNull(inviteId1);

            createInviteRequest = new CreateInviteRequest { ReceiverPlayerId = PlayerId2 };
            var inviteId2 = _inviteClient
                .CreateInvite(createInviteRequest, new Metadata { { PitRequestHeaderName, senderPit } })
                .InviteId;
            Assert.NotNull(inviteId2);

            // Verify that the receiver can delete invites.
            var receiverPit1 = CreatePlayerIdentityTokenForPlayer(PlayerId1);
            _inviteClient.DeleteInvite(new DeleteInviteRequest { InviteId = inviteId1 },
                new Metadata { { PitRequestHeaderName, receiverPit1 } });
            var exception = Assert.Throws<RpcException>(() => _inviteClient.GetInvite(
                new GetInviteRequest { InviteId = inviteId1 },
                new Metadata { { PitRequestHeaderName, receiverPit1 } }));
            Assert.AreEqual(StatusCode.NotFound, exception.StatusCode);

            // Verify that the sender can delete invites.
            _inviteClient.DeleteInvite(new DeleteInviteRequest { InviteId = inviteId2 },
                new Metadata { { PitRequestHeaderName, senderPit } });
            exception = Assert.Throws<RpcException>(() => _inviteClient.GetInvite(
                new GetInviteRequest { InviteId = inviteId1 },
                new Metadata { { PitRequestHeaderName, senderPit } }));
            Assert.AreEqual(StatusCode.NotFound, exception.StatusCode);

            // Cleanup.
            _partyClient.DeleteParty(new DeletePartyRequest(), new Metadata { { PitRequestHeaderName, senderPit } });
        }

        [Test]
        public void AllowTheReceiverToAcceptTheInvite()
        {
            // Create a party.
            var senderPit = CreatePlayerIdentityTokenForPlayer(LeaderPlayerId);
            _partyClient.CreateParty(new CreatePartyRequest(), new Metadata { { PitRequestHeaderName, senderPit } });

            // Send invites to other players.
            var createInviteRequest = new CreateInviteRequest { ReceiverPlayerId = PlayerId1 };
            var inviteId = _inviteClient
                .CreateInvite(createInviteRequest, new Metadata { { PitRequestHeaderName, senderPit } })
                .InviteId;
            Assert.NotNull(inviteId);

            // Receiver accepts the invite.
            var receiverPit = CreatePlayerIdentityTokenForPlayer(PlayerId1);
            var getInviteRequest = new GetInviteRequest { InviteId = inviteId };
            var invite = _inviteClient.GetInvite(getInviteRequest, new Metadata { { PitRequestHeaderName, receiverPit } })
                .Invite;
            invite.CurrentStatus = Invite.Types.Status.Accepted;
            var updatedInvite = _inviteClient.UpdateInvite(new UpdateInviteRequest { UpdatedInvite = invite },
                new Metadata { { PitRequestHeaderName, receiverPit } }).Invite;
            Assert.AreEqual(Invite.Types.Status.Accepted, updatedInvite.CurrentStatus);

            // Verify this change has propagated to the sender as well.
            var senderPlayerInvites = _inviteClient.ListAllInvites(new ListAllInvitesRequest(),
                new Metadata { { PitRequestHeaderName, senderPit } });
            Assert.AreEqual(1, senderPlayerInvites.OutboundInvites.Count);
            Assert.AreEqual(updatedInvite, senderPlayerInvites.OutboundInvites[0]);

            // Clean up.
            _inviteClient.DeleteInvite(new DeleteInviteRequest { InviteId = inviteId },
                new Metadata { { PitRequestHeaderName, receiverPit } });
            _partyClient.DeleteParty(new DeletePartyRequest(), new Metadata { { PitRequestHeaderName, senderPit } });
        }

        private static string CreatePlayerIdentityTokenForPlayer(string playerId)
        {
            var request = new CreatePlayerIdentityTokenRequest
            {
                PlayerIdentifier = playerId,
                ProjectName = _projectName,
                Provider = TestProvider
            };
            return _authServiceClient.CreatePlayerIdentityToken(request).PlayerIdentityToken;
        }
    }
}
