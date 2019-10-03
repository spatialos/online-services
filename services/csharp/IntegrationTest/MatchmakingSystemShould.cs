using System;
using System.Collections.Generic;
using System.Threading;
using Grpc.Core;
using Improbable.OnlineServices.Proto.Gateway;
using Improbable.OnlineServices.Proto.Invite;
using Improbable.OnlineServices.Proto.Party;
using Improbable.SpatialOS.Platform.Common;
using Improbable.SpatialOS.PlayerAuth.V2Alpha1;
using MemoryStore.Redis;
using NUnit.Framework;

namespace IntegrationTest
{
    public class MatchmakingSystemShould
    {
        private const string RedisConnection = "localhost:6379";
        private const string GatewayTarget = "localhost:4040";
        private const string PartyTarget = "localhost:4041";

        private const string LeaderPlayerId = "leader_id";
        private const string MemberPlayerId = "member_id";
        private const string PitRequestHeaderName = "player-identity-token";

        private string _projectName;
        private string _leaderPit;
        private PartyService.PartyServiceClient _partyClient;
        private InviteService.InviteServiceClient _inviteClient;
        private GatewayService.GatewayServiceClient _gatewayClient;
        private PlayerAuthServiceClient _authServiceClient;
        private Metadata _leaderMetadata;

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
            _authServiceClient = PlayerAuthServiceClient.Create(
                credentials: new PlatformRefreshTokenCredential(refreshToken));
            _leaderPit = CreatePlayerIdentityTokenForPlayer(LeaderPlayerId);
            _partyClient = new PartyService.PartyServiceClient(new Channel(PartyTarget, ChannelCredentials.Insecure));
            _inviteClient = new InviteService.InviteServiceClient(new Channel(PartyTarget, ChannelCredentials.Insecure));
            _gatewayClient =
                new GatewayService.GatewayServiceClient(new Channel(GatewayTarget, ChannelCredentials.Insecure));
            _leaderMetadata = new Metadata { { PitRequestHeaderName, _leaderPit } };
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
            var exception = Assert.Throws<RpcException>(() => _gatewayClient.Join(new JoinRequest()));
            Assert.AreEqual(StatusCode.PermissionDenied, exception.StatusCode);
        }

        [Test]
        public void AllowAJoinRequestToBeDeleted()
        {
            // Create a solo party.
            _partyClient.CreateParty(new CreatePartyRequest(), _leaderMetadata);

            // Join matchmaking.
            _gatewayClient.Join(new JoinRequest
            {
                MatchmakingType = "no_match"
            }, _leaderMetadata);

            // Verify that the party has not been matched yet. 
            var status = _gatewayClient.GetJoinStatus(new GetJoinStatusRequest {PlayerId = LeaderPlayerId}, _leaderMetadata);
            Assert.False(status.Complete);

            // Cancel matchmaking.
            _gatewayClient.CancelJoin(new CancelJoinRequest {PlayerId = LeaderPlayerId}, _leaderMetadata);

            // Verify that there is no more information within the matchmaking system about the party/player.
            var rpcException = Assert.Throws<RpcException>(() =>
                _gatewayClient.GetJoinStatus(new GetJoinStatusRequest {PlayerId = LeaderPlayerId}, _leaderMetadata));
            Assert.AreEqual(StatusCode.NotFound, rpcException.Status.StatusCode);

            // Clean-up.
            _partyClient.DeleteParty(new DeletePartyRequest(), _leaderMetadata);
        }

        [Test]
        public void AllowAJoinRequestToBeDeletedAndNewMembersInvited()
        {
            // Create a solo party and get its ID to later invite another player.
            var partyId = _partyClient.CreateParty(new CreatePartyRequest(), _leaderMetadata).PartyId;

            // Join matchmaking.
            _gatewayClient.Join(new JoinRequest
            {
                MatchmakingType = "no_match"
            }, _leaderMetadata);

            // Verify that the party has not been matched yet. 
            var status = _gatewayClient.GetJoinStatus(new GetJoinStatusRequest {PlayerId = LeaderPlayerId}, _leaderMetadata);
            Assert.False(status.Complete);

            // Cancel matchmaking.
            _gatewayClient.CancelJoin(new CancelJoinRequest {PlayerId = LeaderPlayerId}, _leaderMetadata);

            // Verify that there is no more information within the matchmaking system about the party/player.
            var rpcException = Assert.Throws<RpcException>(() =>
                _gatewayClient.GetJoinStatus(new GetJoinStatusRequest {PlayerId = LeaderPlayerId}, _leaderMetadata));
            Assert.AreEqual(StatusCode.NotFound, rpcException.Status.StatusCode);

            // Verify that we can invite another member to the party
            var pitAnotherMember = CreatePlayerIdentityTokenForPlayer(MemberPlayerId);
            var inviteAnotherPlayer = _inviteClient.CreateInvite(new CreateInviteRequest { ReceiverPlayerId = MemberPlayerId },
                new Metadata {{PitRequestHeaderName, pitAnotherMember}}).InviteId;
            Assert.NotNull(inviteAnotherPlayer);
            _partyClient.JoinParty(new JoinPartyRequest { PartyId = partyId },
                new Metadata { { PitRequestHeaderName, pitAnotherMember } });

            // Join matchmaking for the second time.
            _gatewayClient.Join(new JoinRequest
            {
                MatchmakingType = "no_match"
            }, _leaderMetadata);

            // Verify that the party has not been matched yet. 
            status = _gatewayClient.GetJoinStatus(new GetJoinStatusRequest {PlayerId = LeaderPlayerId}, _leaderMetadata);
            Assert.False(status.Complete);

            // Cancel matchmaking.
            _gatewayClient.CancelJoin(new CancelJoinRequest {PlayerId = LeaderPlayerId}, _leaderMetadata);

            // Clean-up.
            _partyClient.DeleteParty(new DeletePartyRequest(), _leaderMetadata);
        }

        [Test]
        public void PreventPartiesFromQueueingTwice()
        {
            // Create a solo party.
            _partyClient.CreateParty(new CreatePartyRequest(), _leaderMetadata);

            // Join matchmaking.
            _gatewayClient.Join(new JoinRequest
            {
                MatchmakingType = "no_match"
            }, _leaderMetadata);

            // Verify that en exception is thrown if trying to join matchmaking again.
            var rpcException = Assert.Throws<RpcException>(() => _gatewayClient.Join(new JoinRequest
            {
                MatchmakingType = "no_match"
            }, _leaderMetadata));
            Assert.AreEqual(StatusCode.AlreadyExists, rpcException.Status.StatusCode);
            Assert.That(rpcException.Message, Contains.Substring("already queued"));

            // Clean-up.
            _gatewayClient.CancelJoin(new CancelJoinRequest {PlayerId = LeaderPlayerId}, _leaderMetadata);
            _partyClient.DeleteParty(new DeletePartyRequest(), _leaderMetadata);
        }

        [Test]
        public void MatchAMultiPlayerParty()
        {
            // Create a party with multiple players within it.
            var partyId =
                _partyClient.CreateParty(new CreatePartyRequest(), _leaderMetadata)
                    .PartyId;
            var pitAnotherMember = CreatePlayerIdentityTokenForPlayer(MemberPlayerId);
            var inviteAnotherPlayer = _inviteClient.CreateInvite(new CreateInviteRequest { ReceiverPlayerId = MemberPlayerId },
                _leaderMetadata).InviteId;
            Assert.NotNull(inviteAnotherPlayer);
            _partyClient.JoinParty(new JoinPartyRequest { PartyId = partyId },
                new Metadata { { PitRequestHeaderName, pitAnotherMember } });

            // Join matchmaking.
            _gatewayClient.Join(new JoinRequest
            {
                MatchmakingType = "match1"
            }, _leaderMetadata);

            AssertWithinSeconds(10, () =>
            {
                var leaderStatus = _gatewayClient.GetJoinStatus(new GetJoinStatusRequest {PlayerId = LeaderPlayerId}, _leaderMetadata);
                if (!leaderStatus.Complete)
                {
                    return false;
                }

                Assert.AreEqual("test_deployment_1", leaderStatus.DeploymentName);
                Assert.False(string.IsNullOrEmpty(leaderStatus.LoginToken));

                // Verify that the other member has been matched into the same deployment as the leader.
                var otherMemberStatus = _gatewayClient.GetJoinStatus(new GetJoinStatusRequest {PlayerId = MemberPlayerId},
                    new Metadata { { PitRequestHeaderName, pitAnotherMember } });
                Assert.True(otherMemberStatus.Complete);
                Assert.AreEqual("test_deployment_1", otherMemberStatus.DeploymentName);
                Assert.False(string.IsNullOrEmpty(otherMemberStatus.LoginToken));

                return true;
            });

            // Clean-up.
            _partyClient.DeleteParty(new DeletePartyRequest(), _leaderMetadata);
            _inviteClient.DeleteInvite(new DeleteInviteRequest { InviteId = inviteAnotherPlayer }, _leaderMetadata);
        }

        [Test]
        public void MatchASinglePlayerParty()
        {
            // Create a solo party.
            _partyClient.CreateParty(new CreatePartyRequest(), _leaderMetadata);

            // Join matchmaking.
            _gatewayClient.Join(new JoinRequest
            {
                MatchmakingType = "match1"
            }, _leaderMetadata);

            // Verify that the solo-party has been successfully matched to a deployment.
            AssertWithinSeconds(10, () =>
            {
                var status = _gatewayClient.GetJoinStatus(new GetJoinStatusRequest {PlayerId = LeaderPlayerId}, _leaderMetadata);
                if (!status.Complete)
                {
                    return false;
                }

                return status.DeploymentName.Equals("test_deployment_1") &&
                       !string.IsNullOrEmpty(status.LoginToken);
            });

            // Clean-up.
            _partyClient.DeleteParty(new DeletePartyRequest(), _leaderMetadata);
        }

        [Test]
        public void MatchPartiesInABatch()
        {
            var invitesList = new List<string>();
            // Create three parties with a different amount of members. The first one is a solo party, the rest have two
            // and three members respectively.
            var leaderIds = new List<string>();
            var leaderIdToMembers = new Dictionary<string, List<string>>();
            var playerPits = new Dictionary<string, string>();
            for (var partyCount = 1; partyCount <= 3; partyCount++)
            {
                var leaderId = $"leader_{partyCount}";
                leaderIds.Add(leaderId);
                var leaderPit = CreatePlayerIdentityTokenForPlayer(leaderId);
                playerPits[leaderId] = leaderPit;
                var partyId = _partyClient.CreateParty(new CreatePartyRequest(),
                    new Metadata { { PitRequestHeaderName, leaderPit } }).PartyId;

                for (var memberCount = 1; memberCount < partyCount; memberCount++)
                {
                    var memberId = $"member_{partyCount}_{memberCount}";
                    var memberPit = CreatePlayerIdentityTokenForPlayer(memberId);
                    var inviteAnotherPlayer = _inviteClient.CreateInvite(new CreateInviteRequest { ReceiverPlayerId = memberId },
                        new Metadata { { PitRequestHeaderName, leaderPit } }).InviteId;
                    Assert.NotNull(inviteAnotherPlayer);
                    invitesList.Add(inviteAnotherPlayer);

                    leaderIdToMembers.TryAdd(leaderId, new List<string>());
                    leaderIdToMembers[leaderId].Add(memberId);
                    playerPits[memberId] = memberPit;
                    _partyClient.JoinParty(new JoinPartyRequest { PartyId = partyId },
                        new Metadata { { PitRequestHeaderName, memberPit } });
                }
            }

            // The three leaders perform a Join request for their parties.
            foreach (var leaderId in leaderIds)
            {
                _gatewayClient.Join(new JoinRequest
                {
                    MatchmakingType = "match3"
                }, new Metadata { { PitRequestHeaderName, playerPits[leaderId] } });
            }

            AssertWithinSeconds(20, () =>
            {
                foreach (var leaderId in leaderIds)
                {
                    var leaderPit = playerPits[leaderId];
                    var leaderStatus = _gatewayClient.GetJoinStatus(new GetJoinStatusRequest {PlayerId = leaderId},
                        new Metadata {{PitRequestHeaderName, leaderPit}});
                    if (!leaderStatus.Complete)
                    {
                        return false;
                    }

                    // If the matchmaking op is done for the leader, other members' ops should also be completed.
                    var partyStatuses = new List<GetJoinStatusResponse> { leaderStatus };
                    foreach (var memberId in leaderIdToMembers[leaderId])
                    {
                        if (memberId == leaderId)
                        {
                            continue;
                        }

                        var memberStatus = _gatewayClient.GetJoinStatus(new GetJoinStatusRequest {PlayerId = memberId},
                            new Metadata {{PitRequestHeaderName, playerPits[memberId]}});
                        if (!memberStatus.Complete)
                        {
                            Assert.Fail(
                                $"The leader has finalized matchmaking but one of the members ({memberId}) has not");
                            return false;
                        }

                        partyStatuses.Add(memberStatus);
                    }

                    // None of the members should have gotten an Error code.
                    foreach (var status in partyStatuses)
                    {
                        if (status.Status == GetJoinStatusResponse.Types.Status.Error || !string.IsNullOrEmpty(status.Error))
                        {
                            Assert.Fail($"Join status returned error");
                            return false;
                        }
                    }

                    // All members of the party should have the same deployment info as the leader. Their login tokens
                    // should have been generated.
                    var leaderDeployment = leaderStatus.DeploymentName;
                    foreach (var status in partyStatuses)
                    {
                        Assert.AreEqual(leaderDeployment, status.DeploymentName);
                        Assert.False(string.IsNullOrEmpty(status.LoginToken));
                    }
                }

                return true;
            });

            // Delete the three parties which have been created.
            foreach (var leaderId in leaderIds)
            {
                _partyClient.DeleteParty(new DeletePartyRequest(),
                    new Metadata { { PitRequestHeaderName, playerPits[leaderId] } });
            }

        }

        [Test]
        public void MatchARequeuedPlayerEventually()
        {
            // Create a solo party.
            _partyClient.CreateParty(new CreatePartyRequest(), _leaderMetadata);

            // Join matchmaking.
            _gatewayClient.Join(new JoinRequest
            {
                MatchmakingType = "to_requeue"
            }, _leaderMetadata);

            AssertWithinSeconds(25, () =>
            {
                var leaderStatus = _gatewayClient.GetJoinStatus(new GetJoinStatusRequest {PlayerId = LeaderPlayerId}, _leaderMetadata);
                if (!leaderStatus.Complete)
                {
                    return false;
                }

                if (!string.IsNullOrEmpty(leaderStatus.Error))
                {
                    Assert.Fail($"Leader status contained an error");
                    return false;
                }

                if (leaderStatus.DeploymentName.Equals("test_deployment_requeue") && !string.IsNullOrEmpty(leaderStatus.LoginToken))
                {
                    return true;
                }

                Assert.Fail($"User was not matched to correct deployment. Was: {leaderStatus.DeploymentName}");
                return false;
            });

            // Clean up.
            _partyClient.DeleteParty(new DeletePartyRequest(), _leaderMetadata);
        }

        private static void AssertWithinSeconds(int seconds, Func<bool> condition)
        {
            var startTime = DateTime.Now;
            while (DateTime.Now - startTime < TimeSpan.FromSeconds(seconds))
            {
                if (condition())
                {
                    return;
                }

                Thread.Sleep(TimeSpan.FromSeconds(1));
            }

            Assert.Fail("Timed out");
        }

        private string CreatePlayerIdentityTokenForPlayer(string playerId)
        {
            return _authServiceClient.CreatePlayerIdentityToken(new CreatePlayerIdentityTokenRequest
            {
                PlayerIdentifier = playerId,
                Provider = "test_provider",
                ProjectName = _projectName
            }).PlayerIdentityToken;
        }
    }
}
