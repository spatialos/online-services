using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Grpc.Core;
using Improbable.OnlineServices.Common;
using Improbable.OnlineServices.DataModel;
using Improbable.OnlineServices.DataModel.Gateway;
using Improbable.OnlineServices.DataModel.Party;
using Improbable.OnlineServices.Proto.Gateway;
using Improbable.SpatialOS.PlayerAuth.V2Alpha1;
using MemoryStore;
using Serilog;
using JoinRequestProto = Improbable.OnlineServices.Proto.Gateway.JoinRequest;
using PartyDataModel = Improbable.OnlineServices.DataModel.Party.Party;

namespace Gateway
{
    public class GatewayServiceImpl : GatewayService.GatewayServiceBase
    {
        private readonly IMemoryStoreClientManager<IMemoryStoreClient> _memoryStoreClientManager;
        private readonly PlayerAuthServiceClient _playerAuthServiceClient;

        public GatewayServiceImpl(
            IMemoryStoreClientManager<IMemoryStoreClient> memoryStoreClientManager,
            PlayerAuthServiceClient playerAuthServiceClient)
        {
            _memoryStoreClientManager = memoryStoreClientManager;
            _playerAuthServiceClient = playerAuthServiceClient;
        }

        public override async Task<JoinResponse> Join(JoinRequestProto request, ServerCallContext context)
        {
            // TODO(dom): refactor this later.
            var playerIdentifier = AuthHeaders.ExtractPlayerId(context);
            using (var memClient = _memoryStoreClientManager.GetClient())
            {
                try
                {
                    var party = await GetPartyForMember(memClient, playerIdentifier) ??
                                throw new EntryNotFoundException(playerIdentifier);

                    if (party.LeaderPlayerId != playerIdentifier)
                    {
                        throw new RpcException(new Status(StatusCode.PermissionDenied,
                            "Only the leader can start matchmaking"));
                    }

                    if (!party.SufficientMembers())
                    {
                        throw new RpcException(new Status(StatusCode.FailedPrecondition,
                            "There are not enough members in the party to start matchmaking"));
                    }

                    party.CurrentPhase = PartyDataModel.Phase.Matchmaking;

                    var matchmakingMetadata = new Dictionary<string, string>(request.Metadata);
                    var partyJoinRequest = new PartyJoinRequest(party, request.MatchmakingType, matchmakingMetadata);
                    var entriesToCreate = new List<Entry> { partyJoinRequest };
                    entriesToCreate.AddRange(CreateJoinRequestsForEachMember(party, request.MatchmakingType,
                        matchmakingMetadata));

                    using (var tx = memClient.CreateTransaction())
                    {
                        tx.UpdateAll(party.Yield());
                        tx.CreateAll(entriesToCreate);
                        tx.EnqueueAll(partyJoinRequest.Yield());
                    }
                }
                catch (EntryNotFoundException)
                {
                    Log.Information("Player is not a member of any party");
                    throw new RpcException(new Status(StatusCode.NotFound, "player is not a member of any party"));
                }
                catch (EntryAlreadyExistsException e)
                {
                    Reporter.JoinRequestQueuedInc();
                    Log.Information($"Party already queued: {e.Id}.");
                    throw new RpcException(new Status(StatusCode.AlreadyExists,
                        "party is already queued for matchmaking"));
                }
                catch (TransactionAbortedException)
                {
                    Reporter.TransactionAbortedInc("Join");
                    Log.Warning("Transaction for join was aborted");
                    throw new RpcException(new Status(StatusCode.Unavailable,
                        "join aborted due to concurrent modification; safe to retry"));
                }
            }

            Reporter.JoinRequestInc();
            Log.Information($"Created party join request for the party associated to player ${playerIdentifier}.");
            return new JoinResponse();
        }

        public override async Task<GetJoinStatusResponse> GetJoinStatus(GetJoinStatusRequest request, ServerCallContext context)
        {
            var playerIdentity = AuthHeaders.ExtractPlayerId(context);
            if (!string.Equals(request.PlayerId, playerIdentity))
            {
                throw new RpcException(new Status(StatusCode.PermissionDenied,
                    "Fetching another player's join status is forbidden."));
            }

            PlayerJoinRequest joinRequest;
            using (var memClient = _memoryStoreClientManager.GetClient())
            {
                try
                {
                    joinRequest = await memClient.GetAsync<PlayerJoinRequest>(request.PlayerId) ??
                                  throw new EntryNotFoundException(request.PlayerId);
                    if (joinRequest.IsComplete())
                    {
                        using (var tx = memClient.CreateTransaction())
                        {
                            tx.DeleteAll(joinRequest.Yield());
                        }
                    }
                }
                catch (EntryNotFoundException e)
                {
                    Reporter.JoinStatusNotFoundInc();
                    Log.Warning($"Join request for {e.Id} does not exist");
                    throw new RpcException(new Status(StatusCode.NotFound, "requested player does not exist"));
                }
                catch (TransactionAbortedException)
                {
                    Reporter.TransactionAbortedInc("GetJoinStatus");
                    Log.Warning("Transaction for join request deletion was aborted");
                    throw new RpcException(new Status(StatusCode.Unavailable,
                        "deletion aborted due to concurrent modification; safe to retry"));
                }
            }

            var resp = new GetJoinStatusResponse
            {
                Complete = joinRequest.IsComplete(),
                Status = MatchStateToJoinStatus(joinRequest.State)
            };

            switch (resp.Status)
            {
                case GetJoinStatusResponse.Types.Status.Joined:
                    resp.DeploymentName = joinRequest.DeploymentName;
                    resp.LoginToken = await CreateLoginTokenForDeployment(joinRequest.DeploymentId, joinRequest.PlayerIdentityToken);
                    break;
                case GetJoinStatusResponse.Types.Status.Error:
                    resp.Error = "the join request encountered an error";
                    break;
            }

            Reporter.JoinStatusInc(joinRequest.State);
            if (resp.Complete)
            {
                Log.Information($"Join request for {request.PlayerId} done in state {joinRequest.State}.");
            }

            return resp;
        }

        public override async Task<CancelJoinResponse> CancelJoin(CancelJoinRequest request, ServerCallContext context)
        {
            var playerIdentity = AuthHeaders.ExtractPlayerId(context);
            if (!string.Equals(request.PlayerId, playerIdentity))
            {
                throw new RpcException(new Status(StatusCode.PermissionDenied,
                    "Removing another player from the queue is forbidden."));
            }

            Log.Information($"Requested cancellation for the party of player identifier {request.PlayerId}.");
            using (var memClient = _memoryStoreClientManager.GetClient())
            {
                var party = await GetPartyOfMember(memClient, request.PlayerId);
                if (party == null)
                {
                    throw new RpcException(new Status(StatusCode.NotFound,
                        "The player making this call is not a member of any party"));
                }

                if (party.LeaderPlayerId != request.PlayerId)
                {
                    throw new RpcException(new Status(StatusCode.PermissionDenied,
                        "Only the leader can delete a matchmaking join request"));
                }

                try
                {
                    var partyJoinRequest = await memClient.GetAsync<PartyJoinRequest>(party.Id) ??
                                           throw new EntryNotFoundException(party.Id);

                    var toDelete = new List<Entry> { partyJoinRequest };
                    foreach (var (member, _) in partyJoinRequest.Party.MemberIdToPit)
                    {
                        toDelete.Add(await memClient.GetAsync<PlayerJoinRequest>(member) ??
                                     throw new EntryNotFoundException(member));
                    }

                    party.CurrentPhase = PartyDataModel.Phase.Forming;

                    using (var tx = memClient.CreateTransaction())
                    {
                        tx.UpdateAll(party.Yield());
                        tx.RemoveAllFromQueue(partyJoinRequest.Yield());
                        tx.DeleteAll(toDelete);
                    }

                    Reporter.CancelJoinInc();
                    return new CancelJoinResponse();
                }
                catch (EntryNotFoundException exception)
                {
                    Log.Warning($"Delete for {request.PlayerId} failed.");
                    if (exception.Id.Contains(party.Id))
                    {
                        Reporter.CancelJoinNotFoundInc();
                        throw new RpcException(new Status(StatusCode.NotFound,
                            "requested party is not in matchmaking"));
                    }

                    throw new RpcException(new Status(StatusCode.Internal,
                        $"could not find join request for player {exception.Id}"));
                }
                catch (TransactionAbortedException)
                {
                    Reporter.TransactionAbortedInc("CancelJoin");
                    Log.Warning("Transaction for join cancellation was aborted");
                    throw new RpcException(new Status(StatusCode.Unavailable,
                        "deletion aborted due to concurrent modification; safe to retry"));
                }
            }
        }

        private async Task<string> CreateLoginTokenForDeployment(string deploymentId, string playerIdentityToken)
        {
            try
            {
                var loginTokenResp = await _playerAuthServiceClient.CreateLoginTokenAsync(new CreateLoginTokenRequest
                {
                    DeploymentId = deploymentId,
                    PlayerIdentityToken = playerIdentityToken
                });
                return loginTokenResp.LoginToken;
            }
            catch (Exception e)
            {
                Log.Error(e, "encountered an error creating a login token");
                throw new RpcException(new Status(StatusCode.Internal, "failed to create login token for deployment"));
            }
        }

        private static async Task<PartyDataModel> GetPartyForMember(IMemoryStoreClient memClient, string playerId)
        {
            var member = await memClient.GetAsync<Member>(playerId);
            if (member == null)
            {
                return null;
            }

            return await memClient.GetAsync<PartyDataModel>(member.PartyId);
        }

        private static IEnumerable<PlayerJoinRequest> CreateJoinRequestsForEachMember(PartyDataModel party,
            string matchmakingType, Dictionary<string, string> matchmakingMetadata)
        {
            var entriesToCreate = new List<PlayerJoinRequest>();
            foreach (var (memberId, memberPit) in party.MemberIdToPit)
            {
                entriesToCreate.Add(new PlayerJoinRequest(memberId, memberPit, matchmakingType, matchmakingMetadata));
            }

            return entriesToCreate;
        }

        private static async Task<PartyDataModel> GetPartyOfMember(IMemoryStoreClient memClient, string playerId)
        {
            var member = await memClient.GetAsync<Member>(playerId);
            return member == null ? null : await memClient.GetAsync<PartyDataModel>(member.PartyId);
        }

        private static GetJoinStatusResponse.Types.Status MatchStateToJoinStatus(MatchState state)
        {
            switch (state)
            {
                case MatchState.Requested:
                    return GetJoinStatusResponse.Types.Status.Waiting;
                case MatchState.Matching:
                    return GetJoinStatusResponse.Types.Status.Matching;
                case MatchState.Matched:
                    return GetJoinStatusResponse.Types.Status.Joined;
                case MatchState.Error:
                    return GetJoinStatusResponse.Types.Status.Error;
                default:
                    return GetJoinStatusResponse.Types.Status.UnknownStatus;
            }
        }
    }
}
