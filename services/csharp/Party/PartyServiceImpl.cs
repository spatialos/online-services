using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Grpc.Core;
using Improbable.MetagameServices.Common;
using Improbable.MetagameServices.DataModel;
using Improbable.MetagameServices.DataModel.Party;
using Improbable.MetagameServices.Proto.Invite;
using Improbable.MetagameServices.Proto.Party;
using MemoryStore;
using Serilog;
using Invite = Improbable.MetagameServices.DataModel.Party.Invite;
using PartyProto = Improbable.MetagameServices.Proto.Party.Party;
using PartyDataModel = Improbable.MetagameServices.DataModel.Party.Party;
using PartyPhaseProto = Improbable.MetagameServices.Proto.Party.Party.Types.Phase;
using PartyPhaseDataModel = Improbable.MetagameServices.DataModel.Party.Party.Phase;

namespace Party
{
    // TODO(OS-185): Transition to context based status codes instead of RpcExceptions.
    public class PartyServiceImpl : PartyService.PartyServiceBase
    {
        private readonly IMemoryStoreClientManager<IMemoryStoreClient> _memoryStoreClientManager;

        public PartyServiceImpl(IMemoryStoreClientManager<IMemoryStoreClient> memoryStoreClientManager)
        {
            _memoryStoreClientManager = memoryStoreClientManager;
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

        public override async Task<DeletePartyResponse> DeleteParty(DeletePartyRequest request, ServerCallContext context)
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
                    throw new RpcException(new Status(StatusCode.FailedPrecondition, "The player is not invited to this party"));
                }
                var invited = (await Task.WhenAll(playerInvites.InboundInviteIds
                        .Select(invite => memClient.GetAsync<Invite>(invite))))
                        .Where(invite =>
                        {
                            if (invite == null)
                            {
                                Log.Logger.Warning("Failed to fetch an invite for {player}", playerId);
                            }
                            return invite != null;
                        })
                        .Any(invite => invite.CurrentStatus == Invite.Status.Pending && invite.ReceiverId == playerId);

                if (!invited)
                {
                    throw new RpcException(new Status(StatusCode.FailedPrecondition, "The player is not invited to this party"));
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

                var initiator = initiatorTask.Result ?? throw new RpcException(new Status(StatusCode.NotFound, "The initiator player is not a member of any party"));

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
