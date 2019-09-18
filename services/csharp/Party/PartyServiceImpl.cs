using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Grpc.Core;
using Improbable.OnlineServices.Common;
using Improbable.OnlineServices.Common.Analytics;
using Improbable.OnlineServices.DataModel;
using Improbable.OnlineServices.DataModel.Party;
using Improbable.OnlineServices.Proto.Party;
using MemoryStore;
using Serilog;
using Invite = Improbable.OnlineServices.DataModel.Party.Invite;
using PartyProto = Improbable.OnlineServices.Proto.Party.Party;
using PartyDataModel = Improbable.OnlineServices.DataModel.Party.Party;
using PartyPhaseProto = Improbable.OnlineServices.Proto.Party.Party.Types.Phase;
using PartyPhaseDataModel = Improbable.OnlineServices.DataModel.Party.Party.Phase;

namespace Party
{
    // TODO(OS-185): Transition to context based status codes instead of RpcExceptions.
    public class PartyServiceImpl : PartyService.PartyServiceBase
    {
        private readonly IMemoryStoreClientManager<IMemoryStoreClient> _memoryStoreClientManager;
        private readonly AnalyticsSenderClassWrapper _analytics;

        public PartyServiceImpl(IMemoryStoreClientManager<IMemoryStoreClient> memoryStoreClientManager,
            IAnalyticsSender analytics = null)
        {
            _memoryStoreClientManager = memoryStoreClientManager;
            _analytics = (analytics ?? new NullAnalyticsSender()).WithEventClass("party");
        }

        public override Task<CreatePartyResponse> CreateParty(CreatePartyRequest request, ServerCallContext context)
        {
            var playerId = AuthHeaders.ExtractPlayerId(context);
            var pit = AuthHeaders.ExtractPit(context);

            // TODO(iuliaharasim/dom): Move logic specific to party creation in a separate class.
            PartyDataModel party;
            try
            {
                party = new PartyDataModel(playerId, pit, request.MinMembers, request.MaxMembers, request.Metadata);
            }
            catch (ArgumentException exception)
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, exception.Message));
            }

            var leader = party.GetLeader();

            using (var memClient = _memoryStoreClientManager.GetClient())
            using (var transaction = memClient.CreateTransaction())
            {
                transaction.CreateAll(new List<Entry> { party, leader });
            }

            IDictionary<string, string> eventAttributes = new Dictionary<string, string>
            {
                { "partyId", party.Id }
            };
            string[] eventTypes = { "player_created_party", "player_joined_party", "party_created" };
            foreach (string eventType in eventTypes)
            {
                if (eventType == "party_created")
                {
                    eventAttributes.Add(new KeyValuePair<string, string>("partyPhase", party.CurrentPhase.ToString()));
                    _analytics.Send(eventType, (Dictionary<string, string>) eventAttributes, playerId);
                }
                else
                {
                    _analytics.Send(eventType, (Dictionary<string, string>) eventAttributes, playerId);
                }
            }

            return Task.FromResult(new CreatePartyResponse { PartyId = party.Id });
        }

        public override async Task<GetPartyByPlayerIdResponse> GetPartyByPlayerId(GetPartyByPlayerIdRequest request,
            ServerCallContext context)
        {
            var playerId = AuthHeaders.ExtractPlayerId(context);

            using (var memClient = _memoryStoreClientManager.GetClient())
            {
                var party = await GetPartyByPlayerId(memClient, playerId) ??
                            throw new RpcException(new Status(StatusCode.NotFound,
                                "The player is not a member of any party"));
                return new GetPartyByPlayerIdResponse { Party = ConvertToProto(party) };
            }
        }

        public override async Task<DeletePartyResponse> DeleteParty(DeletePartyRequest request,
            ServerCallContext context)
        {
            var playerId = AuthHeaders.ExtractPlayerId(context);

            using (var memClient = _memoryStoreClientManager.GetClient())
            {
                var party = await GetPartyByPlayerId(memClient, playerId) ??
                            throw new RpcException(new Status(StatusCode.NotFound,
                                "The player is not a member of any party"));
                if (playerId != party.LeaderPlayerId)
                {
                    throw new RpcException(new Status(StatusCode.PermissionDenied,
                        "Cannot delete party: player needs to be the leader of the party"));
                }

                // TODO(iuliaharasim/dom): Move logic specific to party deletion in a separate class.
                var entitiesToDelete = new List<Entry> { party };
                entitiesToDelete.AddRange(party.GetMembers());

                try
                {
                    using (var transaction = memClient.CreateTransaction())
                    {
                        transaction.DeleteAll(entitiesToDelete);
                    }
                }
                catch (EntryNotFoundException exception)
                {
                    if (exception.Id.Contains(party.Id))
                    {
                        throw;
                    }

                    // If one of the members has left the party, it is safe to retry this RPC.
                    throw new TransactionAbortedException();
                }

                string[] eventTypes = { "player_cancelled_party", "party_cancelled" };
                foreach (string eventType in eventTypes)
                {
                    _analytics.Send(eventType, new Dictionary<string, string>
                    {
                        { "partyId", party.Id }
                     }, playerId);
                }

                foreach (var m in party.GetMembers())
                {
                    _analytics.Send(
                        "player_left_cancelled_party", new Dictionary<string, string>
                        {
                            { "partyId", party.Id }
                        }, m.Id);
                }
            }

            return new DeletePartyResponse();
        }

        public override async Task<JoinPartyResponse> JoinParty(JoinPartyRequest request, ServerCallContext context)
        {
            var playerId = AuthHeaders.ExtractPlayerId(context);

            var pit = AuthHeaders.ExtractPit(context);

            if (string.IsNullOrEmpty(request.PartyId))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument,
                    "JoinParty requires a non-empty party id"));
            }

            using (var memClient = _memoryStoreClientManager.GetClient())
            {
                var partyToJoin = await memClient.GetAsync<PartyDataModel>(request.PartyId) ??
                                  throw new RpcException(new Status(StatusCode.NotFound, "The party doesn't exist"));

                var member = await memClient.GetAsync<Member>(playerId);
                if (member != null && member.PartyId != request.PartyId)
                {
                    throw new RpcException(new Status(StatusCode.AlreadyExists,
                        "The player is a member of another party"));
                }

                var playerInvites = await memClient.GetAsync<PlayerInvites>(playerId);
                if (playerInvites == null)
                {
                    throw new RpcException(new Status(StatusCode.FailedPrecondition,
                        "The player is not invited to this party"));
                }

                var invites = (await Task.WhenAll(playerInvites.InboundInviteIds
                        .Select(invite => memClient.GetAsync<Invite>(invite))))
                    .Where(invite =>
                    {
                        if (invite == null)
                        {
                            Log.Logger.Warning("Failed to fetch an invite for {player}", playerId);
                        }

                        return invite != null;
                    }).ToList();

                var invited = invites
                    .Any(invite => invite.CurrentStatus == Invite.Status.Pending && invite.ReceiverId == playerId);

                if (!invited)
                {
                    throw new RpcException(new Status(StatusCode.FailedPrecondition,
                        "The player is not invited to this party"));
                }


                if (partyToJoin.CurrentPhase != PartyPhaseDataModel.Forming)
                {
                    throw new RpcException(new Status(StatusCode.FailedPrecondition,
                        "The party is no longer in the Forming phase"));
                }

                // TODO(iuliaharasim/dom): Move logic specific to joining a party into a separate class.
                try
                {
                    var added = partyToJoin.AddPlayerToParty(playerId, pit);
                    // If false, the player already joined the party so we should terminate early.
                    if (!added)
                    {
                        return new JoinPartyResponse { Party = ConvertToProto(partyToJoin) };
                    }
                }
                catch (Exception exception)
                {
                    throw new RpcException(new Status(StatusCode.FailedPrecondition, exception.Message));
                }

                using (var transaction = memClient.CreateTransaction())
                {
                    transaction.CreateAll(new List<Entry> { partyToJoin.GetMember(playerId) });
                    transaction.UpdateAll(new List<Entry> { partyToJoin });
                }

                _analytics.Send("player_joined_party", new Dictionary<string, object>
                {
                    { "partyId", partyToJoin.Id },
                    {
                        "invites", invites.Select(invite => new Dictionary<string, string>
                        {
                            { "inviteId", invite.Id },
                            { "playerIdInviter", invite.SenderId }
                        })
                    }
                }, playerId);

                return new JoinPartyResponse { Party = ConvertToProto(partyToJoin) };
            }
        }

        public override async Task<LeavePartyResponse> LeaveParty(LeavePartyRequest request, ServerCallContext context)
        {
            var playerId = AuthHeaders.ExtractPlayerId(context);

            await LeaveParty(playerId);
            return new LeavePartyResponse();
        }

        public override async Task<KickOutPlayerResponse> KickOutPlayer(KickOutPlayerRequest request,
            ServerCallContext context)
        {
            var playerId = AuthHeaders.ExtractPlayerId(context);

            if (string.IsNullOrEmpty(request.EvictedPlayerId))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument,
                    "LeaveParty requires a non-empty evicted player id"));
            }

            if (playerId == request.EvictedPlayerId)
            {
                await LeaveParty(request.EvictedPlayerId);
                return new KickOutPlayerResponse();
            }

            using (var memClient = _memoryStoreClientManager.GetClient())
            {
                var initiatorTask = memClient.GetAsync<Member>(playerId);
                var evictedTask = memClient.GetAsync<Member>(request.EvictedPlayerId);
                Task.WaitAll(initiatorTask, evictedTask);

                var initiator = initiatorTask.Result ?? throw new RpcException(new Status(StatusCode.NotFound,
                                    "The initiator player is not a member of any party"));

                // If the evicted has already left the party, we should return early.
                var evicted = evictedTask.Result;
                if (evicted == null)
                {
                    return new KickOutPlayerResponse();
                }

                var party = await memClient.GetAsync<PartyDataModel>(initiator.PartyId) ??
                            throw new RpcException(new Status(StatusCode.NotFound,
                                "The party no longer exists"));

                if (party.LeaderPlayerId != initiator.Id)
                {
                    throw new RpcException(new Status(StatusCode.PermissionDenied,
                        "The initiator is not the leader of the party"));
                }

                if (initiator.PartyId != evicted.PartyId)
                {
                    throw new RpcException(new Status(StatusCode.PermissionDenied,
                        "The players are not members of the same party"));
                }

                // TODO(iuliaharasim/dom): Move logic specific to removing a player from a party into a separate class.
                // If false, the player has already been removed from the party so we should terminate early.
                if (!party.RemovePlayerFromParty(evicted.Id))
                {
                    return new KickOutPlayerResponse();
                }

                using (var transaction = memClient.CreateTransaction())
                {
                    transaction.DeleteAll(new List<Entry> { evicted });
                    transaction.UpdateAll(new List<Entry> { party });
                }

                _analytics.Send("player_kicked_from_party", new Dictionary<string, string>
                {
                    { "partyId", party.Id },
                    { "playerIdKicker", playerId }
                }, evicted.Id);
            }

            return new KickOutPlayerResponse();
        }

        private async Task LeaveParty(string playerId)
        {
            using (var memClient = _memoryStoreClientManager.GetClient())
            {
                var memberToDelete = await memClient.GetAsync<Member>(playerId);
                // We should terminate early if the player has already left the party.
                if (memberToDelete == null)
                {
                    return;
                }

                var party = await memClient.GetAsync<PartyDataModel>(memberToDelete.PartyId) ??
                            throw new RpcException(new Status(StatusCode.NotFound,
                                "The party no longer exists"));

                try
                {
                    // We should terminate early if the player has already left the party.
                    if (!party.RemovePlayerFromParty(playerId))
                    {
                        return;
                    }
                }
                catch (Exception exception)
                {
                    throw new RpcException(new Status(StatusCode.FailedPrecondition, exception.Message));
                }

                // TODO(iuliaharasim/dom): Move logic specific to leaving a party into a separate class.
                using (var transaction = memClient.CreateTransaction())
                {
                    transaction.DeleteAll(new List<Entry> { memberToDelete });
                    transaction.UpdateAll(new List<Entry> { party });
                }

                _analytics.Send("player_left_party", new Dictionary<string, string>
                {
                    { "partyId", party.Id }
                }, playerId);
            }
        }

        // Updates the Party's information, excluding its member list.
        // TODO: Move to FieldMasks.
        public override async Task<UpdatePartyResponse> UpdateParty(UpdatePartyRequest request,
            ServerCallContext context)
        {
            var playerId = AuthHeaders.ExtractPlayerId(context);

            ValidateUpdatePartyRequest(request);
            using (var memClient = _memoryStoreClientManager.GetClient())
            {
                var updatedParty = request.UpdatedParty;
                var party = await memClient.GetAsync<PartyDataModel>(updatedParty.Id) ??
                            throw new RpcException(new Status(StatusCode.NotFound,
                                "There is no such party with the given id"));

                if (party.LeaderPlayerId != playerId)
                {
                    throw new RpcException(new Status(StatusCode.PermissionDenied,
                        "The update operation can only be done by the leader of the party"));
                }

                if (!party.UpdatePartyLeader(updatedParty.LeaderPlayerId))
                {
                    throw new RpcException(new Status(StatusCode.FailedPrecondition,
                        "The proposed new leader is not a member of the party"));
                }

                if (!party.UpdateMinMaxMembers(updatedParty.MinMembers, updatedParty.MaxMembers))
                {
                    throw new RpcException(new Status(StatusCode.FailedPrecondition,
                        "Encountered error while updating the minimum and maximum amount of members"));
                }

                // TODO(iuliaharasim/dom): Move logic specific to updating a party into a separate class.
                party.CurrentPhase = ConvertToDataModel(updatedParty.CurrentPhase);
                party.UpdateMetadata(updatedParty.Metadata);

                using (var transaction = memClient.CreateTransaction())
                {
                    transaction.UpdateAll(new List<Entry> { party });
                }

                IDictionary<string, object> eventAttributes = new Dictionary<string, object>
                {
                    { "partyId", updatedParty.Id },
                    {
                        "newPartyState", new Dictionary<string, object>
                        {
                            { "partyLeaderId", updatedParty.LeaderPlayerId },
                            { "maxMembers", updatedParty.MaxMembers },
                            { "minMembers", updatedParty.MinMembers }
                        }
                    }
                };
                string[] eventTypes = { "player_updated_party", "party_updated" };
                foreach (string eventType in eventTypes)
                {
                    if (eventType == "party_updated")
                    {
                        eventAttributes.Add(new KeyValuePair<string, object>("partyPhase", updatedParty.CurrentPhase.ToString()));
                        _analytics.Send(eventType, (Dictionary<string, object>) eventAttributes, playerId);
                    }
                    else
                    {
                        _analytics.Send(eventType, (Dictionary<string, object>) eventAttributes, playerId);
                    }
                }

                return new UpdatePartyResponse { Party = ConvertToProto(party) };
            }
        }

        private static void ValidateUpdatePartyRequest(UpdatePartyRequest request)
        {
            var updatedParty = request.UpdatedParty ??
                               throw new RpcException(new Status(StatusCode.InvalidArgument,
                                   "UpdatePartyInfo requires a non-empty updated party"));
            if (string.IsNullOrEmpty(updatedParty.Id))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument,
                    "UpdatePartyInfo requires an updated party with a non-empty id"));
            }
        }

        private static async Task<PartyDataModel> GetPartyByPlayerId(IMemoryStoreClient memClient, string playerId)
        {
            var member = await memClient.GetAsync<Member>(playerId);
            if (member == null)
            {
                return null;
            }

            return await memClient.GetAsync<PartyDataModel>(member.PartyId);
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

        private static PartyPhaseProto ConvertToProto(PartyPhaseDataModel phase)
        {
            switch (phase)
            {
                case PartyPhaseDataModel.Forming:
                    return PartyPhaseProto.Forming;
                case PartyPhaseDataModel.Matchmaking:
                    return PartyPhaseProto.Matchmaking;
                case PartyPhaseDataModel.InGame:
                    return PartyPhaseProto.InGame;
                default:
                    return PartyPhaseProto.Unknown;
            }
        }

        private static PartyPhaseDataModel ConvertToDataModel(PartyPhaseProto phase)
        {
            switch (phase)
            {
                case PartyPhaseProto.Forming:
                    return PartyPhaseDataModel.Forming;
                case PartyPhaseProto.Matchmaking:
                    return PartyPhaseDataModel.Matchmaking;
                case PartyPhaseProto.InGame:
                    return PartyPhaseDataModel.InGame;
                default:
                    return PartyPhaseDataModel.Unknown;
            }
        }
    }
}
