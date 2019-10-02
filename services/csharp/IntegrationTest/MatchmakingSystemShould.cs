using System;
using System.Collections.Generic;
using System.Threading;
using Google.Api.Gax.Grpc;
using Google.LongRunning;
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
        private OperationsClient _operationsClient;
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
            _operationsClient = OperationsClient.Create(new Channel(GatewayTarget, ChannelCredentials.Insecure));
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
            var op = _gatewayClient.Join(new JoinRequest
            {
                MatchmakingType = "no_match"
            }, _leaderMetadata);
            Assert.AreEqual(LeaderPlayerId, op.Name);
            Assert.False(op.Done);

            // Verify that the party has not been matched yet. 
            var fetchedOp = _operationsClient.GetOperation(LeaderPlayerId,
                CallSettings.FromHeader(PitRequestHeaderName, _leaderPit));
            Assert.AreEqual(LeaderPlayerId, fetchedOp.Name);
            Assert.False(fetchedOp.Done);

            // Cancel matchmaking.
            _operationsClient.DeleteOperation(LeaderPlayerId,
                CallSettings.FromHeader(PitRequestHeaderName, _leaderPit));

            // Verify that there is no more information within the matchmaking system about the party/player.
            var rpcException = Assert.Throws<RpcException>(() =>
                _operationsClient.GetOperation(LeaderPlayerId,
                    CallSettings.FromHeader(PitRequestHeaderName, _leaderPit)));
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
            var op = _gatewayClient.Join(new JoinRequest
            {
                MatchmakingType = "no_match"
            }, _leaderMetadata);
            Assert.AreEqual(LeaderPlayerId, op.Name);
            Assert.False(op.Done);

            // Verify that the party has not been matched yet. 
            var fetchedOp = _operationsClient.GetOperation(LeaderPlayerId,
                CallSettings.FromHeader(PitRequestHeaderName, _leaderPit));
            Assert.AreEqual(LeaderPlayerId, fetchedOp.Name);
            Assert.False(fetchedOp.Done);

            // Cancel matchmaking.
            _operationsClient.DeleteOperation(LeaderPlayerId,
                CallSettings.FromHeader(PitRequestHeaderName, _leaderPit));

            // Verify that there is no more information within the matchmaking system about the party/player.
            var rpcException = Assert.Throws<RpcException>(() =>
                _operationsClient.GetOperation(LeaderPlayerId,
                    CallSettings.FromHeader(PitRequestHeaderName, _leaderPit)));
            Assert.AreEqual(StatusCode.NotFound, rpcException.Status.StatusCode);

            // Verify that we can invite another member to the party
            var pitAnotherMember = CreatePlayerIdentityTokenForPlayer(MemberPlayerId);
            var inviteAnotherPlayer = _inviteClient.CreateInvite(new CreateInviteRequest { ReceiverPlayerId = MemberPlayerId },
                _leaderMetadata).InviteId;
            Assert.NotNull(inviteAnotherPlayer);
            _partyClient.JoinParty(new JoinPartyRequest { PartyId = partyId },
                new Metadata { { PitRequestHeaderName, pitAnotherMember } });

            // Join matchmaking for the second time.
            var opSecond = _gatewayClient.Join(new JoinRequest
            {
                MatchmakingType = "no_match"
            }, _leaderMetadata);
            Assert.AreEqual(LeaderPlayerId, opSecond.Name);
            Assert.False(opSecond.Done);

            // Verify that the party has not been matched yet. 
            fetchedOp = _operationsClient.GetOperation(LeaderPlayerId,
                CallSettings.FromHeader(PitRequestHeaderName, _leaderPit));
            Assert.AreEqual(LeaderPlayerId, fetchedOp.Name);
            Assert.False(fetchedOp.Done);

            // Cancel matchmaking.
            _operationsClient.DeleteOperation(LeaderPlayerId,
                CallSettings.FromHeader(PitRequestHeaderName, _leaderPit));

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
            _operationsClient.DeleteOperation(LeaderPlayerId,
                CallSettings.FromHeader(PitRequestHeaderName, _leaderPit));
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
                var leaderOp = _operationsClient.GetOperation(LeaderPlayerId,
                    CallSettings.FromHeader(PitRequestHeaderName, _leaderPit));
                if (!leaderOp.Done)
                {
                    return false;
                }

                var assignment = leaderOp.Response.Unpack<JoinResponse>();
                Assert.AreEqual("test_deployment_1", assignment.DeploymentName);
                Assert.False(string.IsNullOrEmpty(assignment.LoginToken));

                // Verify that the other member has been matched into the same deployment as the leader.
                var otherMemberOp = _operationsClient.GetOperation(MemberPlayerId,
                    CallSettings.FromHeader(PitRequestHeaderName, CreatePlayerIdentityTokenForPlayer(MemberPlayerId)));
                Assert.True(otherMemberOp.Done);
                var memberAssignment = otherMemberOp.Response.Unpack<JoinResponse>();
                Assert.AreEqual("test_deployment_1", memberAssignment.DeploymentName);
                Assert.False(string.IsNullOrEmpty(memberAssignment.LoginToken));

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
                var op = _operationsClient.GetOperation(LeaderPlayerId,
                    CallSettings.FromHeader(PitRequestHeaderName, _leaderPit));
                if (!op.Done)
                {
                    return false;
                }

                var assignment = op.Response.Unpack<JoinResponse>();
                return assignment.DeploymentName.Equals("test_deployment_1") &&
                       !string.IsNullOrEmpty(assignment.LoginToken);
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
                    var leaderOp = _operationsClient.GetOperation(leaderId,
                        CallSettings.FromHeader(PitRequestHeaderName, leaderPit));
                    if (!leaderOp.Done)
                    {
                        return false;
                    }

                    // If the matchmaking op is done for the leader, other members' ops should also be completed.
                    var partyOps = new List<Operation> { leaderOp };
                    foreach (var memberId in leaderIdToMembers[leaderId])
                    {
                        if (memberId == leaderId)
                        {
                            continue;
                        }

                        var memberOp = _operationsClient.GetOperation(memberId,
                            CallSettings.FromHeader(PitRequestHeaderName, playerPits[memberId]));
                        if (!memberOp.Done)
                        {
                            Assert.Fail(
                                $"The leader has finalized matchmaking but one of the members ({memberId}) has not");
                            return false;
                        }

                        partyOps.Add(memberOp);
                    }

                    // None of the members should have gotten an Error code.
                    foreach (var op in partyOps)
                    {
                        if (op.Error != null)
                        {
                            Assert.Fail($"Op returned error code: {op.Error.Code}");
                            return false;
                        }
                    }

                    // All members of the party should have the same deployment info as the leader. Their login tokens
                    // should have been generated.
                    var leaderDeployment = leaderOp.Response.Unpack<JoinResponse>().DeploymentName;
                    foreach (var op in partyOps)
                    {
                        var joinResponse = op.Response.Unpack<JoinResponse>();
                        Assert.AreEqual(leaderDeployment, joinResponse.DeploymentName);
                        Assert.False(string.IsNullOrEmpty(joinResponse.LoginToken));
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
                var op = _operationsClient.GetOperation(LeaderPlayerId,
                    CallSettings.FromHeader(PitRequestHeaderName, _leaderPit));
                if (!op.Done)
                {
                    return false;
                }

                if (op.Error != null)
                {
                    Assert.Fail($"Op returned error code: {op.Error.Code}");
                    return false;
                }

                var res = op.Response.Unpack<JoinResponse>();
                if (res.DeploymentName.Equals("test_deployment_requeue") && !string.IsNullOrEmpty(res.LoginToken))
                {
                    return true;
                }

                Assert.Fail($"User was not matched to correct deployment. Was: {res.DeploymentName}");
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
