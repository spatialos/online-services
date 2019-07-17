using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Google.Api.Gax.Grpc;
using Google.LongRunning;
using Grpc.Core;
using Improbable.MetagameServices.Proto.Gateway;
using Improbable.MetagameServices.Proto.Invite;
using Improbable.MetagameServices.Proto.Party;
using Improbable.SpatialOS.Platform.Common;
using Improbable.SpatialOS.PlayerAuth.V2Alpha1;
using NUnit.Framework;

namespace IntegrationTest
{
    public class GatewayPerformanceShould
    {
        private const string GatewayTarget = "localhost:4040";
        private const string PartyTarget = "localhost:4041";

        private const string PitRequestHeaderName = "player-identity-token";

        private string _projectName;
        private List<PartyService.PartyServiceClient> _partyClients;
        private List<InviteService.InviteServiceClient> _inviteClients;
        private List<GatewayService.GatewayServiceClient> _gatewayClients;
        private List<OperationsClient> _operationsClients;
        private List<PlayerAuthServiceClient> _authServiceClients;
        private readonly Random random = new Random();
        private readonly int clients = 20;

        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            _projectName = Environment.GetEnvironmentVariable("SPATIAL_PROJECT");
            var _refreshToken = Environment.GetEnvironmentVariable("SPATIAL_REFRESH_TOKEN");

            // Create multiple clients in order to connect to every instance behind the load balancer and spread load.
            _partyClients = new List<PartyService.PartyServiceClient>(clients);
            _inviteClients = new List<InviteService.InviteServiceClient>(clients);
            _authServiceClients = new List<PlayerAuthServiceClient>(clients);
            _operationsClients = new List<OperationsClient>(clients);
            _gatewayClients = new List<GatewayService.GatewayServiceClient>(clients);

            for (var i = 0; i < clients; i++)
            {
                _authServiceClients.Add(PlayerAuthServiceClient.Create(
                    credentials: new PlatformRefreshTokenCredential(_refreshToken)
                ));
                _partyClients.Add(
                    new PartyService.PartyServiceClient(new Channel(PartyTarget, ChannelCredentials.Insecure)));
                _inviteClients.Add(
                    new InviteService.InviteServiceClient(new Channel(PartyTarget, ChannelCredentials.Insecure)));
                _gatewayClients.Add(
                    new GatewayService.GatewayServiceClient(new Channel(GatewayTarget, ChannelCredentials.Insecure)));
                _operationsClients.Add(
                    OperationsClient.Create(new Channel(GatewayTarget, ChannelCredentials.Insecure)));
            }
        }

        private PartyService.PartyServiceClient GetPartyClient()
        {
            return _partyClients[random.Next(clients)];
        }
        private GatewayService.GatewayServiceClient GetGatewayClient()
        {
            return _gatewayClients[random.Next(clients)];
        }
        private InviteService.InviteServiceClient GetInviteClient()
        {
            return _inviteClients[random.Next(clients)];
        }
        private OperationsClient GetOperationsClient()
        {
            return _operationsClients[random.Next(clients)];
        }
        private PlayerAuthServiceClient GetAuthClient()
        {
            return _authServiceClients[random.Next(clients)];
        }

        /**
         * This test requires the entire suite of services to be running, including a matcher which allocates players to a deployment.
         * The matcher doesn't need to mark the deployment as in_use, although it could if it wanted to.
         * Tested against a GKE cluster, 1000 players takes ~100 seconds to complete. Results may differ when running locally.
         * By default, runs against localhost and expects the docker-composed integration services to be running.
         */
        [Test]
        public async Task AllowOneThousandPlayersToMatchAtOnce()
        {
            ThreadPool.SetMinThreads(1000, 1000);
            const int playersPerParty = 1;
            const int parties = 1000;

            var startTime = DateTime.UtcNow;
            var tasks = new List<Task>();
            for (var i = 0; i < playersPerParty * parties; i++)
            {
                var myId = i;
                var playerName = $"test_player_{startTime.ToLongTimeString()}_{myId}";
                var playerPit = await CreatePlayerIdentityTokenForPlayer(playerName);
                var task = Task.Run(async () =>
                {
                    var playerMetadata = new Metadata { { PitRequestHeaderName, playerPit } };
                    string partyId = "";
                    if (myId % playersPerParty == 0)
                    {
                        // Lead player sets up the match and invites the others
                        var party = false;
                        while (!party)
                        {
                            try
                            {
                                GetPartyClient().CreateParty(new CreatePartyRequest(), playerMetadata);
                                party = true;
                            }
                            catch (RpcException e)
                            {
                                Console.WriteLine($"CreateParty exception: {e}");
                            }
                        }

                        for (var j = 0; j < playersPerParty - 1; j++)
                        {
                            var invitedPlayerId = $"test_player_{startTime}_{myId + j + 1}";
                            GetInviteClient().CreateInvite(new CreateInviteRequest
                            {
                                ReceiverPlayerId = invitedPlayerId
                            }, playerMetadata);
                        }

                        int members = 1;
                        while (members < playersPerParty)
                        {
                            var partyData =
                                GetPartyClient().GetPartyByPlayerId(new GetPartyByPlayerIdRequest(), playerMetadata);
                            members = partyData.Party.MemberIds.Count;
                            await Task.Delay(500);
                        }

                        Console.WriteLine($"Enough players joined: Continuing as master ({playerName})");

                        // Join matchmaking.
                        var joined = false;
                        while (!joined)
                        {
                            try
                            {
                                GetGatewayClient().Join(new JoinRequest
                                {
                                    MatchmakingType = "match1"
                                }, playerMetadata);
                                joined = true;
                            }
                            catch (RpcException e)
                            {
                                if (e.StatusCode == StatusCode.AlreadyExists)
                                {
                                    joined = true;
                                }
                                Console.WriteLine($"Exception in Join ({playerName}): {e}");
                            }
                        }
                    }
                    else
                    {
                        // All other players wait for an invite
                        Console.WriteLine("Beginning as player");
                        var joined = false;
                        do
                        {
                            var invites = GetInviteClient().ListAllInvites(new ListAllInvitesRequest(), playerMetadata);
                            if (invites.InboundInvites.Count > 0)
                            {
                                _ = GetPartyClient().JoinParty(new JoinPartyRequest
                                {
                                    PartyId = invites.InboundInvites[0].PartyId
                                }, playerMetadata);
                                joined = true;
                            }
                        } while (!joined);
                    }
                });
                var contTask = task.ContinueWith(async t =>
                {
                    // Non-leaders may not have started matchmaking yet so GetOperation could fail a few times.
                    Operation op = null;
                    do
                    {
                        try
                        {
                            op = GetOperationsClient().GetOperation(new GetOperationRequest { Name = playerName },
                                CallSettings.FromHeader(PitRequestHeaderName, playerPit));
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($"Failed to poll: {e}");
                        }

                        await Task.Delay(100);
                    } while (op == null || !op.Done);
                });
                contTask.ContinueWith(t =>
                {
                    var playerMetadata = new Metadata { { PitRequestHeaderName, playerPit } };
                    GetPartyClient().DeleteParty(new DeletePartyRequest(), playerMetadata);
                });
                tasks.Add(task);
            }

            Task.WaitAll(tasks.ToArray());

            var seconds = (DateTime.UtcNow - startTime).TotalSeconds;
            Console.WriteLine($"Test completed in {seconds}s");
            Assert.Less(seconds, TimeSpan.FromMinutes(2).TotalSeconds);
        }

        private async Task<string> CreatePlayerIdentityTokenForPlayer(string playerId)
        {
            var resp = await GetAuthClient().CreatePlayerIdentityTokenAsync(new CreatePlayerIdentityTokenRequest
            {
                PlayerIdentifier = playerId,
                Provider = "test_provider",
                ProjectName = _projectName
            });
            return resp.PlayerIdentityToken;
        }
    }
}