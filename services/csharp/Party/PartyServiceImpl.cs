using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Grpc.Core;
using Improbable.OnlineServices.Common;
using Improbable.OnlineServices.DataModel;
using Improbable.OnlineServices.DataModel.Party;
using Improbable.OnlineServices.Proto.Invite;
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

        public override Task<GetPartyByPlayerIdResponse> GetPartyByPlayerId(GetPartyByPlayerIdRequest request,
            ServerCallContext context)
        {
            var playerId = AuthHeaders.ExtractPlayerId(context);

            using (var memClient = _memoryStoreClientManager.GetClient())
            {
                var party = GetPartyByPlayerId(memClient, playerId) ??
                            throw new RpcException(new Status(StatusCode.NotFound,
                                "The player is not a member of any party"));
                return Task.FromResult(new GetPartyByPlayerIdResponse { Party = ConvertToProto(party) });
            }
        }

        public override Task<DeletePartyResponse> DeleteParty(DeletePartyRequest request, ServerCallContext context)
        {
            var playerId = AuthHeaders.ExtractPlayerId(context);

            using (var memClient = _memoryStoreClientManager.GetClient())
            {
                var party = GetPartyByPlayerId(memClient, playerId) ??
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

            return Task.FromResult(new DeletePartyResponse());
        }

        public override Task<JoinPartyResponse> JoinParty(JoinPartyRequest request, ServerCallContext context)
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
                var partyToJoin = memClient.Get<PartyDataModel>(request.PartyId) ??
                                  throw new RpcException(new Status(StatusCode.NotFound, "The party doesn't exist"));

                var member = memClient.Get<Member>(playerId);
                if (member != null && member.PartyId != request.PartyId)
                {
                    throw new RpcException(new Status(StatusCode.AlreadyExists,
                        "The player is a member of another party"));
                }

                var playerInvites = memClient.Get<PlayerInvites>(playerId);
                if (playerInvites == null)
                {
                    throw new RpcException(new Status(StatusCode.FailedPrecondition, "The player is not invited to this party"));
                }
                var invited = playerInvites.InboundInviteIds
                        .Select(invite => memClient.Get<Invite>(invite))
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
                        return Task.FromResult(new JoinPartyResponse { Party = ConvertToProto(partyToJoin) });
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

                return Task.FromResult(new JoinPartyResponse { Party = ConvertToProto(partyToJoin) });
            }
        }

        public override Task<LeavePartyResponse> LeaveParty(LeavePartyRequest request, ServerCallContext context)
        {
            var playerId = AuthHeaders.ExtractPlayerId(context);

            LeaveParty(playerId);
            return Task.FromResult(new LeavePartyResponse());
        }

        public override Task<KickOutPlayerResponse> KickOutPlayer(KickOutPlayerRequest request,
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
                LeaveParty(request.EvictedPlayerId);
                return Task.FromResult(new KickOutPlayerResponse());
            }

            using (var memClient = _memoryStoreClientManager.GetClient())
            {
                var initiator = memClient.Get<Member>(playerId) ??
                                throw new RpcException(new Status(StatusCode.NotFound,
                                    "The initiator player is not a member of any party"));
                var evicted = memClient.Get<Member>(request.EvictedPlayerId);
                // If the evicted has already left the party, we should return early.
                if (evicted == null)
                {
                    return Task.FromResult(new KickOutPlayerResponse());
                }

                var party = memClient.Get<PartyDataModel>(initiator.PartyId) ??
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
                    return Task.FromResult(new KickOutPlayerResponse());
                }

                using (var transaction = memClient.CreateTransaction())
                {
                    transaction.DeleteAll(new List<Entry> { evicted });
                    transaction.UpdateAll(new List<Entry> { party });
                }
            }

            return Task.FromResult(new KickOutPlayerResponse());
        }

        private void LeaveParty(string playerId)
        {
            using (var memClient = _memoryStoreClientManager.GetClient())
            {
                var memberToDelete = memClient.Get<Member>(playerId);
                // We should terminate early if the player has already left the party.
                if (memberToDelete == null)
                {
                    return;
                }

                var party = memClient.Get<PartyDataModel>(memberToDelete.PartyId) ??
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
        public override Task<UpdatePartyResponse> UpdateParty(UpdatePartyRequest request,
            ServerCallContext context)
        {
            var playerId = AuthHeaders.ExtractPlayerId(context);

            ValidateUpdatePartyRequest(request);
            using (var memClient = _memoryStoreClientManager.GetClient())
            {
                var updatedParty = request.UpdatedParty;
                var party = memClient.Get<PartyDataModel>(updatedParty.Id) ??
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

                return Task.FromResult(new UpdatePartyResponse { Party = ConvertToProto(party) });
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

        private static PartyDataModel GetPartyByPlayerId(IMemoryStoreClient memClient, string playerId)
        {
            var member = memClient.Get<Member>(playerId);
            if (member == null)
            {
                return null;
            }

            return memClient.Get<PartyDataModel>(member.PartyId);
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
                MemberIdToPit = { party.MemberIdToPit },
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