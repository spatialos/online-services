using System;
using System.Collections.Generic;
using Grpc.Core;
using Improbable.OnlineServices.Proto.Invite;
using Improbable.OnlineServices.Proto.Party;
using Improbable.SpatialOS.Platform.Common;
using Improbable.SpatialOS.PlayerAuth.V2Alpha1;
using MemoryStore.Redis;
using NUnit.Framework;
using PartyPhaseProto = Improbable.OnlineServices.Proto.Party.Party.Types.Phase;

namespace IntegrationTest
{
    [TestFixture]
    public class PartySystemShould
    {
        private const string RedisConnection = "localhost:6379";
        private const string PartyServerTarget = "127.0.0.1:4041";
        private const string TestProvider = "test_provider";

        private const string LeaderPlayerId = "Leader";
        private const string PlayerId = "AnotherPlayer";
        private const uint MinMembers = 2;
        private const uint MaxMembers = 3;
        private const string PitRequestHeaderName = "player-identity-token";

        private static string _projectName;
        private static PlayerAuthServiceClient _authServiceClient;
        private static PartyService.PartyServiceClient _partyClient;
        private static InviteService.InviteServiceClient _inviteClient;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _projectName = Environment.GetEnvironmentVariable("SPATIAL_PROJECT");
            var refreshToken = Environment.GetEnvironmentVariable("SPATIAL_REFRESH_TOKEN");
            _authServiceClient =
                PlayerAuthServiceClient.Create(credentials: new PlatformRefreshTokenCredential(refreshToken));
            var channel = new Channel(PartyServerTarget, ChannelCredentials.Insecure);
            _partyClient = new PartyService.PartyServiceClient(channel);
            _inviteClient = new InviteService.InviteServiceClient(channel);
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
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
            var exception = Assert.Throws<RpcException>(() => _partyClient.CreateParty(new CreatePartyRequest()));
            Assert.AreEqual(StatusCode.PermissionDenied, exception.StatusCode);
        }

        [Test]
        public void CreatePartyAndAssociateLeaderToIt()
        {
            // Create a party.
            var pit = CreatePlayerIdentityTokenForPlayer(LeaderPlayerId);
            var createPartyRequest = new CreatePartyRequest { MinMembers = MinMembers, MaxMembers = MaxMembers };
            var partyId = _partyClient.CreateParty(createPartyRequest, new Metadata { { PitRequestHeaderName, pit } })
                .PartyId;

            // Verify that the party was appropriately stored and is associated to the player that created it.
            var partyAssociatedToPlayer = _partyClient
                .GetPartyByPlayerId(new GetPartyByPlayerIdRequest(), new Metadata { { PitRequestHeaderName, pit } })
                .Party;
            Assert.AreEqual(partyId, partyAssociatedToPlayer.Id);
            Assert.AreEqual(LeaderPlayerId, partyAssociatedToPlayer.LeaderPlayerId);
            Assert.AreEqual(MinMembers, partyAssociatedToPlayer.MinMembers);
            Assert.AreEqual(MaxMembers, partyAssociatedToPlayer.MaxMembers);

            // Clean up.
            _partyClient.DeleteParty(new DeletePartyRequest(), new Metadata { { PitRequestHeaderName, pit } });
        }

        [Test]
        public void LetOtherMembersJoinAParty()
        {
            // Create a party.
            var pitLeader = CreatePlayerIdentityTokenForPlayer(LeaderPlayerId);
            var createPartyRequest = new CreatePartyRequest { MinMembers = MinMembers, MaxMembers = MaxMembers };
            var partyId = _partyClient
                .CreateParty(createPartyRequest, new Metadata { { PitRequestHeaderName, pitLeader } }).PartyId;

            // Verify that the party was successfully created.
            var partyAssociatedToPlayer = _partyClient
                .GetPartyByPlayerId(new GetPartyByPlayerIdRequest(),
                    new Metadata { { PitRequestHeaderName, pitLeader } })
                .Party;
            Assert.NotNull(partyAssociatedToPlayer);

            // Verify that another player can successfully join the party.
            var pitAnotherPlayer = CreatePlayerIdentityTokenForPlayer(PlayerId);
            var inviteAnotherPlayer = _inviteClient.CreateInvite(new CreateInviteRequest { ReceiverPlayerId = PlayerId },
                new Metadata { { PitRequestHeaderName, pitLeader } }).InviteId;
            Assert.NotNull(inviteAnotherPlayer);
            var joinRequest = new JoinPartyRequest { PartyId = partyId };
            var partyJoined = _partyClient
                .JoinParty(joinRequest, new Metadata { { PitRequestHeaderName, pitAnotherPlayer } }).Party;
            Assert.AreEqual(2, partyJoined.MemberIdToPit.Count);

            // Rejoining the same party should be allowed but the number of players should remain the same.
            partyJoined = _partyClient
                .JoinParty(joinRequest, new Metadata { { PitRequestHeaderName, pitAnotherPlayer } }).Party;
            Assert.AreEqual(2, partyJoined.MemberIdToPit.Count);

            // Clean up.
            _partyClient.DeleteParty(new DeletePartyRequest(), new Metadata { { PitRequestHeaderName, pitLeader } });

            // Verify that its former members are no longer associated to the party since it has been deleted.
            var exception = Assert.Throws<RpcException>(() =>
                _partyClient.GetPartyByPlayerId(new GetPartyByPlayerIdRequest(),
                    new Metadata { { PitRequestHeaderName, pitLeader } }));
            Assert.AreEqual(StatusCode.NotFound, exception.StatusCode);

            exception = Assert.Throws<RpcException>(() =>
                _partyClient.GetPartyByPlayerId(new GetPartyByPlayerIdRequest(),
                    new Metadata { { PitRequestHeaderName, pitAnotherPlayer } }));
            Assert.AreEqual(StatusCode.NotFound, exception.StatusCode);
        }

        [Test]
        public void PreventPlayersFromJoiningAPartyIfTheyAreMembersOfAnotherParty()
        {
            // Create a party.
            var pitLeader = CreatePlayerIdentityTokenForPlayer(LeaderPlayerId);
            var createPartyRequest = new CreatePartyRequest { MinMembers = MinMembers, MaxMembers = MaxMembers };
            var partyId = _partyClient
                .CreateParty(createPartyRequest, new Metadata { { PitRequestHeaderName, pitLeader } }).PartyId;

            // Verify that the party was successfully created.
            var partyAssociatedToPlayer = _partyClient
                .GetPartyByPlayerId(new GetPartyByPlayerIdRequest(),
                    new Metadata { { PitRequestHeaderName, pitLeader } })
                .Party;
            Assert.NotNull(partyAssociatedToPlayer);

            // Create another party.
            var pitAnotherPlayer = CreatePlayerIdentityTokenForPlayer(PlayerId);
            var inviteAnotherPlayer = _inviteClient.CreateInvite(new CreateInviteRequest { ReceiverPlayerId = PlayerId },
                new Metadata { { PitRequestHeaderName, pitLeader } });
            Assert.NotNull(inviteAnotherPlayer);
            createPartyRequest = new CreatePartyRequest { MinMembers = MinMembers, MaxMembers = MaxMembers };
            _partyClient.CreateParty(createPartyRequest, new Metadata { { PitRequestHeaderName, pitAnotherPlayer } });

            // Verify that the party was successfully created.
            partyAssociatedToPlayer = _partyClient
                .GetPartyByPlayerId(new GetPartyByPlayerIdRequest(),
                    new Metadata { { PitRequestHeaderName, pitAnotherPlayer } })
                .Party;
            Assert.NotNull(partyAssociatedToPlayer);

            // Verify that the second player is not allowed to join another party.
            var joinRequest = new JoinPartyRequest { PartyId = partyId };
            var exception = Assert.Throws<RpcException>(() =>
                _partyClient.JoinParty(joinRequest, new Metadata { { PitRequestHeaderName, pitAnotherPlayer } }));
            Assert.AreEqual(StatusCode.AlreadyExists, exception.StatusCode);

            // Clean up.
            _partyClient.DeleteParty(new DeletePartyRequest(), new Metadata { { PitRequestHeaderName, pitLeader } });
            _partyClient.DeleteParty(new DeletePartyRequest(),
                new Metadata { { PitRequestHeaderName, pitAnotherPlayer } });
        }

        [Test]
        public void PreventPlayersFromJoiningAPartyIfItIsAtFullCapacity()
        {
            // Create a party.
            var pitLeader = CreatePlayerIdentityTokenForPlayer(LeaderPlayerId);
            var createPartyRequest = new CreatePartyRequest { MinMembers = MinMembers, MaxMembers = MaxMembers };
            var partyId = _partyClient
                .CreateParty(createPartyRequest, new Metadata { { PitRequestHeaderName, pitLeader } }).PartyId;

            // Verify that the party was successfully created.
            var partyAssociatedToPlayer = _partyClient
                .GetPartyByPlayerId(new GetPartyByPlayerIdRequest(),
                    new Metadata { { PitRequestHeaderName, pitLeader } })
                .Party;
            Assert.NotNull(partyAssociatedToPlayer);

            // Fill up the party such that it is at full capacity.
            for (var i = 1; i < MaxMembers; i++)
            {
                var playerId = $"{PlayerId}:{i}";
                var pit = CreatePlayerIdentityTokenForPlayer(playerId);
                var inviteAnotherPlayer = _inviteClient.CreateInvite(new CreateInviteRequest { ReceiverPlayerId = playerId },
                    new Metadata { { PitRequestHeaderName, pitLeader } });
                Assert.NotNull(inviteAnotherPlayer);
                var joinRequest = new JoinPartyRequest { PartyId = partyId };
                _partyClient.JoinParty(joinRequest, new Metadata { { PitRequestHeaderName, pit } });
            }

            // Verify that it is impossible for another player to join the party.
            var extraPlayerPit = CreatePlayerIdentityTokenForPlayer(PlayerId);
            var inviteExtraPlayer = _inviteClient.CreateInvite(new CreateInviteRequest { ReceiverPlayerId = PlayerId },
                new Metadata { { PitRequestHeaderName, pitLeader } });
            Assert.NotNull(inviteExtraPlayer);
            var extraJoinRequest = new JoinPartyRequest { PartyId = partyId };
            var exception = Assert.Throws<RpcException>(() =>
                _partyClient.JoinParty(extraJoinRequest, new Metadata { { PitRequestHeaderName, extraPlayerPit } }));
            Assert.AreEqual(StatusCode.FailedPrecondition, exception.StatusCode);
            Assert.That(exception.Message, Contains.Substring("full capacity"));

            // Clean up.
            _partyClient.DeleteParty(new DeletePartyRequest(), new Metadata { { PitRequestHeaderName, pitLeader } });
        }

        [Test]
        public void PreventPlayersFromJoiningAPartyWithoutAValidInvite()
        {
            // Create a party.
            var pitLeader = CreatePlayerIdentityTokenForPlayer(LeaderPlayerId);
            var createPartyRequest = new CreatePartyRequest { MinMembers = MinMembers, MaxMembers = MaxMembers };
            var partyId = _partyClient
                .CreateParty(createPartyRequest, new Metadata { { PitRequestHeaderName, pitLeader } }).PartyId;

            // Verify that the party was successfully created.
            var partyAssociatedToPlayer = _partyClient.GetPartyByPlayerId(new GetPartyByPlayerIdRequest(),
                    new Metadata { { PitRequestHeaderName, pitLeader } }).Party;
            Assert.NotNull(partyAssociatedToPlayer);

            // Verify that another player can successfully join the party.
            var joinRequest = new JoinPartyRequest { PartyId = partyId };
            var exception = Assert.Throws<RpcException>(() =>
                _partyClient.JoinParty(joinRequest, new Metadata { { PitRequestHeaderName, CreatePlayerIdentityTokenForPlayer(PlayerId) } }));
            Assert.AreEqual(StatusCode.FailedPrecondition, exception.StatusCode);
            Assert.That(exception.Message, Contains.Substring("not invited"));

            // Clean up.
            _partyClient.DeleteParty(new DeletePartyRequest(), new Metadata { { PitRequestHeaderName, pitLeader } });
        }

        [Test]
        public void PreventNonLeadersFromDeletingAParty()
        {
            // Create a party.
            var pitLeader = CreatePlayerIdentityTokenForPlayer(LeaderPlayerId);
            var createPartyRequest = new CreatePartyRequest { MinMembers = MinMembers, MaxMembers = MaxMembers };
            var partyId = _partyClient
                .CreateParty(createPartyRequest, new Metadata { { PitRequestHeaderName, pitLeader } }).PartyId;

            // Verify that the party was successfully created.
            var partyAssociatedToPlayer = _partyClient
                .GetPartyByPlayerId(new GetPartyByPlayerIdRequest(),
                    new Metadata { { PitRequestHeaderName, pitLeader } })
                .Party;
            Assert.NotNull(partyAssociatedToPlayer);

            // Another player joins the party.
            var pitAnotherPlayer = CreatePlayerIdentityTokenForPlayer(PlayerId);
            var inviteAnotherPlayer = _inviteClient.CreateInvite(new CreateInviteRequest { ReceiverPlayerId = PlayerId },
                new Metadata { { PitRequestHeaderName, pitLeader } });
            Assert.NotNull(inviteAnotherPlayer);
            var joinRequest = new JoinPartyRequest { PartyId = partyId };
            _partyClient.JoinParty(joinRequest, new Metadata { { PitRequestHeaderName, pitAnotherPlayer } });

            // Verify that it is impossible for the non-leader to delete the party.
            var exception = Assert.Throws<RpcException>(() =>
                _partyClient.DeleteParty(new DeletePartyRequest(),
                    new Metadata { { PitRequestHeaderName, pitAnotherPlayer } }));
            Assert.AreEqual(StatusCode.PermissionDenied, exception.StatusCode);
            Assert.That(exception.Message, Contains.Substring("player needs to be the leader"));

            // Clean up.
            _partyClient.DeleteParty(new DeletePartyRequest(), new Metadata { { PitRequestHeaderName, pitLeader } });
        }

        [Test]
        public void AllowLeadersToAppointOtherPlayersAsLeader()
        {
            // Create a party.
            var pitLeader = CreatePlayerIdentityTokenForPlayer(LeaderPlayerId);
            var metadata = new Dictionary<string, string> { { "random", "things" } };
            var createPartyRequest = new CreatePartyRequest { MinMembers = MinMembers, MaxMembers = MaxMembers };
            createPartyRequest.Metadata.Add(metadata);
            var partyId = _partyClient
                .CreateParty(createPartyRequest, new Metadata { { PitRequestHeaderName, pitLeader } }).PartyId;

            // Another player joins the party.
            var pitAnotherPlayer = CreatePlayerIdentityTokenForPlayer(PlayerId);
            var inviteAnotherPlayer = _inviteClient.CreateInvite(new CreateInviteRequest { ReceiverPlayerId = PlayerId },
                new Metadata { { PitRequestHeaderName, pitLeader } });
            Assert.NotNull(inviteAnotherPlayer);
            var joinRequest = new JoinPartyRequest { PartyId = partyId };
            _partyClient.JoinParty(joinRequest, new Metadata { { PitRequestHeaderName, pitAnotherPlayer } });

            var partyAssociatedToPlayer = _partyClient
                .GetPartyByPlayerId(new GetPartyByPlayerIdRequest(),
                    new Metadata { { PitRequestHeaderName, pitLeader } })
                .Party;

            // Appoint the newly joined player as the leader of the party.
            partyAssociatedToPlayer.LeaderPlayerId = PlayerId;
            var updatedParty = _partyClient.UpdateParty(new UpdatePartyRequest { UpdatedParty = partyAssociatedToPlayer },
                new Metadata { { PitRequestHeaderName, pitLeader } }).Party;

            // Verify that only the leader player id has been updated.
            Assert.AreEqual(PlayerId, updatedParty.LeaderPlayerId);
            Assert.AreEqual(MinMembers, updatedParty.MinMembers);
            Assert.AreEqual(MaxMembers, updatedParty.MaxMembers);
            Assert.AreEqual(PartyPhaseProto.Forming, updatedParty.CurrentPhase);
            CollectionAssert.AreEquivalent(createPartyRequest.Metadata, updatedParty.Metadata);

            // Verify that the leader no longer has the necessary permissions to perform certain operations.
            var exception = Assert.Throws<RpcException>(() =>
                _partyClient.DeleteParty(new DeletePartyRequest(),
                    new Metadata { { PitRequestHeaderName, pitLeader } }));
            Assert.AreEqual(StatusCode.PermissionDenied, exception.StatusCode);

            updatedParty.CurrentPhase = PartyPhaseProto.Matchmaking;
            exception = Assert.Throws<RpcException>(() =>
                _partyClient.UpdateParty(new UpdatePartyRequest { UpdatedParty = updatedParty },
                    new Metadata { { PitRequestHeaderName, pitLeader } }));
            Assert.AreEqual(StatusCode.PermissionDenied, exception.StatusCode);

            // Clean up.
            _partyClient.DeleteParty(new DeletePartyRequest(),
                new Metadata { { PitRequestHeaderName, pitAnotherPlayer } });
        }

        [Test]
        public void AllowLeadersToKickOutOtherPlayers()
        {
            // Create a party.
            var pitLeader = CreatePlayerIdentityTokenForPlayer(LeaderPlayerId);
            var createPartyRequest = new CreatePartyRequest { MinMembers = MinMembers, MaxMembers = MaxMembers };
            var partyId = _partyClient
                .CreateParty(createPartyRequest, new Metadata { { PitRequestHeaderName, pitLeader } }).PartyId;

            // Another player joins the party.
            var pitAnotherPlayer = CreatePlayerIdentityTokenForPlayer(PlayerId);
            var inviteAnotherPlayer = _inviteClient.CreateInvite(new CreateInviteRequest { ReceiverPlayerId = PlayerId },
                new Metadata { { PitRequestHeaderName, pitLeader } });
            Assert.NotNull(inviteAnotherPlayer);
            var joinRequest = new JoinPartyRequest { PartyId = partyId };
            _partyClient.JoinParty(joinRequest, new Metadata { { PitRequestHeaderName, pitAnotherPlayer } });

            // Verify that the player who has recently joined the party is associated to it.
            var party = _partyClient.GetPartyByPlayerId(new GetPartyByPlayerIdRequest(),
                new Metadata { { PitRequestHeaderName, pitAnotherPlayer } }).Party;
            Assert.NotNull(party);
            Assert.AreEqual(LeaderPlayerId, party.LeaderPlayerId);

            // Verify that the leader can kick out other players.
            _partyClient.KickOutPlayer(new KickOutPlayerRequest { EvictedPlayerId = PlayerId },
                new Metadata { { PitRequestHeaderName, pitLeader } });

            var exception = Assert.Throws<RpcException>(
                () => _partyClient.GetPartyByPlayerId(new GetPartyByPlayerIdRequest(),
                    new Metadata { { PitRequestHeaderName, pitAnotherPlayer } }));
            Assert.AreEqual(StatusCode.NotFound, exception.StatusCode);

            // Clean up.
            _partyClient.DeleteParty(new DeletePartyRequest(), new Metadata { { PitRequestHeaderName, pitLeader } });
        }

        [Test]
        public void PreventNonLeadersFromKickingOutOtherMembers()
        {
            // Create a party.
            var pitLeader = CreatePlayerIdentityTokenForPlayer(LeaderPlayerId);
            var metadata = new Dictionary<string, string> { { "random", "things" } };
            var createPartyRequest = new CreatePartyRequest { MinMembers = MinMembers, MaxMembers = MaxMembers };
            createPartyRequest.Metadata.Add(metadata);
            var partyId = _partyClient
                .CreateParty(createPartyRequest, new Metadata { { PitRequestHeaderName, pitLeader } }).PartyId;

            // Another player joins the party.
            var pitAnotherPlayer = CreatePlayerIdentityTokenForPlayer(PlayerId);
            var inviteAnotherPlayer = _inviteClient.CreateInvite(new CreateInviteRequest { ReceiverPlayerId = PlayerId },
                new Metadata { { PitRequestHeaderName, pitLeader } });
            Assert.NotNull(inviteAnotherPlayer);
            var joinRequest = new JoinPartyRequest { PartyId = partyId };
            _partyClient.JoinParty(joinRequest, new Metadata { { PitRequestHeaderName, pitAnotherPlayer } });

            // Verify that the player who has recently joined the party is associated to it.
            var party = _partyClient.GetPartyByPlayerId(new GetPartyByPlayerIdRequest(),
                new Metadata { { PitRequestHeaderName, pitAnotherPlayer } }).Party;
            Assert.NotNull(party);
            Assert.AreEqual(LeaderPlayerId, party.LeaderPlayerId);

            // Verify that the leader can kick out other players.
            _partyClient.KickOutPlayer(new KickOutPlayerRequest { EvictedPlayerId = PlayerId },
                new Metadata { { PitRequestHeaderName, pitLeader } });

            var exception = Assert.Throws<RpcException>(
                () => _partyClient.GetPartyByPlayerId(new GetPartyByPlayerIdRequest(),
                    new Metadata { { PitRequestHeaderName, pitAnotherPlayer } }));
            Assert.AreEqual(StatusCode.NotFound, exception.StatusCode);

            // Clean up.
            _partyClient.DeleteParty(new DeletePartyRequest(), new Metadata { { PitRequestHeaderName, pitLeader } });
        }

        [Test]
        public void RandomlyAssignAnotherLeaderWhenTheyLeaveTheParty()
        {
            // Create a party.
            var pitLeader = CreatePlayerIdentityTokenForPlayer(LeaderPlayerId);
            var metadata = new Dictionary<string, string> { { "random", "things" } };
            var createPartyRequest = new CreatePartyRequest { MinMembers = MinMembers, MaxMembers = MaxMembers };
            createPartyRequest.Metadata.Add(metadata);
            var partyId = _partyClient
                .CreateParty(createPartyRequest, new Metadata { { PitRequestHeaderName, pitLeader } }).PartyId;

            // Another player joins the party.
            var pitAnotherPlayer = CreatePlayerIdentityTokenForPlayer(PlayerId);
            var inviteAnotherPlayer = _inviteClient.CreateInvite(new CreateInviteRequest { ReceiverPlayerId = PlayerId },
                new Metadata { { PitRequestHeaderName, pitLeader } });
            Assert.NotNull(inviteAnotherPlayer);
            var joinRequest = new JoinPartyRequest { PartyId = partyId };
            _partyClient.JoinParty(joinRequest, new Metadata { { PitRequestHeaderName, pitAnotherPlayer } });

            // The leader leaves the party.
            _partyClient.LeaveParty(new LeavePartyRequest(), new Metadata { { PitRequestHeaderName, pitLeader } });

            // Verify that the former-leader is no longer associated to any party.
            _partyClient.LeaveParty(new LeavePartyRequest(), new Metadata { { PitRequestHeaderName, pitLeader } });
            var exception = Assert.Throws<RpcException>(
                () => _partyClient.GetPartyByPlayerId(new GetPartyByPlayerIdRequest(),
                    new Metadata { { PitRequestHeaderName, pitLeader } }));
            Assert.AreEqual(StatusCode.NotFound, exception.StatusCode);

            // Verify that the other remaining player has automatically been assigned the role of 'leader' since there
            // are no other members in the party.
            var partyAfterLeaderLeft = _partyClient
                .GetPartyByPlayerId(new GetPartyByPlayerIdRequest(),
                    new Metadata { { PitRequestHeaderName, pitAnotherPlayer } }).Party;
            Assert.AreEqual(PlayerId, partyAfterLeaderLeft.LeaderPlayerId);

            // Clean up.
            _partyClient.DeleteParty(new DeletePartyRequest(),
                new Metadata { { PitRequestHeaderName, pitAnotherPlayer } });
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