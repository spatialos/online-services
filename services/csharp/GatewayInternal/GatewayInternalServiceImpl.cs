using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Google.Protobuf;
using Grpc.Core;
using Improbable.MetagameServices.DataModel;
using Improbable.MetagameServices.DataModel.Gateway;
using Improbable.MetagameServices.Proto.Gateway;
using MemoryStore;
using Serilog;
using PartyProto = Improbable.MetagameServices.Proto.Party.Party;
using PartyDataModel = Improbable.MetagameServices.DataModel.Party.Party;

namespace GatewayInternal
{
    public class GatewayInternalServiceImpl : GatewayInternalService.GatewayInternalServiceBase
    {
        private readonly IMemoryStoreClientManager<IMemoryStoreClient> _matchmakingMemoryStoreClientManager;

        public GatewayInternalServiceImpl(
            IMemoryStoreClientManager<IMemoryStoreClient> matchmakingMemoryStoreClientManager)
        {
            _matchmakingMemoryStoreClientManager = matchmakingMemoryStoreClientManager;
        }

        public override async Task<AssignDeploymentsResponse> AssignDeployments(AssignDeploymentsRequest request,
            ServerCallContext context)
        {
            try
            {
                using (var memClient = _matchmakingMemoryStoreClientManager.GetClient())
                {
                    var toUpdate = new List<Entry>();
                    foreach (var assignment in request.Assignments)
                    {
                        Reporter.AssignDeploymentInc(assignment.DeploymentId, assignment.Result);
                        foreach (var memberId in assignment.Party.MemberIds)
                        {
                            var playerJoinRequest = await memClient.GetAsync<PlayerJoinRequest>(memberId);
                            if (playerJoinRequest == null)
                            {
                                continue;
                            }
                            switch (assignment.Result)
                            {
                                case Assignment.Types.Result.Error:
                                    playerJoinRequest.State = MatchState.Error;
                                    break;
                                case Assignment.Types.Result.Matched:
                                    playerJoinRequest.AssignMatch(assignment.DeploymentId, assignment.DeploymentName);
                                    break;
                                case Assignment.Types.Result.Requeued:
                                    playerJoinRequest.State = MatchState.Requested;
                                    break;
                            }

                            toUpdate.Add(playerJoinRequest);
                        }
                    }

                    var toRequeue = new List<PartyJoinRequest>();
                    var toDelete = new List<PartyJoinRequest>();

                    foreach (var assignment in request.Assignments)
                    {
                        var party = assignment.Party;
                        var partyJoinRequest = await memClient.GetAsync<PartyJoinRequest>(party.Id);
                        if (partyJoinRequest == null)
                        {
                            // Party join request has been cancelled.
                            continue;
                        }
                        if (assignment.Result == Assignment.Types.Result.Requeued)
                        {
                            partyJoinRequest.RefreshQueueData();
                            toRequeue.Add(partyJoinRequest);
                            toUpdate.Add(partyJoinRequest);
                        }
                        else
                        {
                            // If the matchmaking process for this party has reached a final state, we should delete the
                            // PartyJoinRequest associated to it.
                            toDelete.Add(partyJoinRequest);
                        }
                    }

                    using (var tx = memClient.CreateTransaction())
                    {
                        tx.UpdateAll(toUpdate);
                        tx.EnqueueAll(toRequeue);
                        tx.DeleteAll(toDelete);
                    }
                }
            }
            catch (EntryNotFoundException e)
            {
                Reporter.AssignDeploymentNotFoundInc(e.Id);
                Log.Warning($"Attempted to assign deployment to nonexistent join request {e.Id}.");
                throw new RpcException(new Status(StatusCode.NotFound, "Join request does not exist"));
            }
            catch (TransactionAbortedException)
            {
                Reporter.TransactionAbortedInc("AssignDeployments");
                Log.Warning("Transaction aborted during deployment assignment.");
                throw new RpcException(new Status(StatusCode.Unavailable,
                    "assignment aborted due to concurrent modification; safe to retry"));
            }

            return new AssignDeploymentsResponse();
        }

        public override async Task<PopWaitingPartiesResponse> PopWaitingParties(PopWaitingPartiesRequest request,
            ServerCallContext context)
        {
            Reporter.GetWaitingPartiesInc(request.NumParties);
            if (request.NumParties == 0)
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "must request at least one party"));
            }

            using (var memClient = _matchmakingMemoryStoreClientManager.GetClient())
            {
                try
                {
                    Task<IEnumerable<string>> dequeuedPartyIds;
                    using (var tx = memClient.CreateTransaction())
                    {
                        dequeuedPartyIds = tx.DequeueAsync(request.Type, request.NumParties);
                    }

                    dequeuedPartyIds.Wait();

                    // TODO investigate best approach to handling this error (leave as null, log warning, ignore?)
                    IEnumerable<PartyJoinRequest> partyJoinRequests;
                    try
                    {
                        partyJoinRequests = dequeuedPartyIds.Result
                            .Select(async id =>
                                await memClient.GetAsync<PartyJoinRequest>(id) ?? throw new EntryNotFoundException(id))
                            .Select(t => t.Result)
                            .ToList();
                    }
                    catch (AggregateException ex)
                    {
                        throw ex.InnerException;
                    }

                    var playerJoinRequestsToUpdate = new List<PlayerJoinRequest>();
                    foreach (var partyJoinRequest in partyJoinRequests)
                    {
                        foreach (var (memberId, _) in partyJoinRequest.Party.MemberIdToPit)
                        {
                            var playerJoinRequest = await memClient.GetAsync<PlayerJoinRequest>(memberId) ??
                                                    throw new EntryNotFoundException(memberId);
                            playerJoinRequest.State = MatchState.Matching;
                            playerJoinRequestsToUpdate.Add(playerJoinRequest);
                        }
                    }

                    using (var tx = memClient.CreateTransaction())
                    {
                        tx.UpdateAll(playerJoinRequestsToUpdate);
                    }

                    var response = new PopWaitingPartiesResponse();
                    foreach (var partyJoinRequest in partyJoinRequests)
                    {
                        response.Parties.Add(ConvertToProto(partyJoinRequest));
                    }

                    return response;
                }
                catch (EntryNotFoundException ex)
                {
                    // TODO: maybe add metrics for this.
                    throw new RpcException(new Status(StatusCode.Internal, $"could not find JoinRequest for {ex.Id}"));
                }
                catch (InsufficientEntriesException)
                {
                    Reporter.InsufficientWaitingPartiesInc(request.NumParties);
                    throw new RpcException(new Status(StatusCode.ResourceExhausted,
                        "requested number of parties players could not be met"));
                }
                catch (TransactionAbortedException)
                {
                    throw new RpcException(new Status(StatusCode.Unavailable,
                        "dequeue aborted due to concurrent modification; safe to retry"));
                }
            }
        }

        private static WaitingParty ConvertToProto(PartyJoinRequest request)
        {
            return new WaitingParty
            {
                Party = ConvertToProto(request.Party),
                Metadata = { request.Metadata }
            };
        }

        private static PartyProto ConvertToProto(PartyDataModel party)
        {
            return new PartyProto
            {
                Id = party.Id,
                LeaderPlayerId = party.LeaderPlayerId,
                MinMembers = party.MinMembers,
                MaxMembers = party.MaxMembers,
                Metadata = { party.Metadata },
                MemberIds = { party.MemberIds },
                CurrentPhase = ConvertToProto(party.CurrentPhase)
            };
        }

        private static PartyProto.Types.Phase ConvertToProto(PartyDataModel.Phase phase)
        {
            switch (phase)
            {
                case PartyDataModel.Phase.Forming:
                    return PartyProto.Types.Phase.Forming;
                case PartyDataModel.Phase.Matchmaking:
                    return PartyProto.Types.Phase.Matchmaking;
                case PartyDataModel.Phase.InGame:
                    return PartyProto.Types.Phase.InGame;
                default:
                    return PartyProto.Types.Phase.Unknown;
            }
        }
    }
}
