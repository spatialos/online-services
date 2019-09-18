using System.Collections.Generic;
using System.Threading.Tasks;
using Grpc.Core;
using Improbable.OnlineServices.Common;
using Improbable.OnlineServices.Common.Analytics;
using Improbable.OnlineServices.DataModel;
using Improbable.OnlineServices.DataModel.Party;
using Improbable.OnlineServices.Proto.Invite;
using MemoryStore;
using PartyDataModel = Improbable.OnlineServices.DataModel.Party.Party;
using InviteProto = Improbable.OnlineServices.Proto.Invite.Invite;
using InviteStatusProto = Improbable.OnlineServices.Proto.Invite.Invite.Types.Status;
using InviteDataModel = Improbable.OnlineServices.DataModel.Party.Invite;
using InviteStatusDataModel = Improbable.OnlineServices.DataModel.Party.Invite.Status;

namespace Party
{
    // TODO(OS-185): Transition to context based status codes instead of RpcExceptions.
    public class InviteServiceImpl : InviteService.InviteServiceBase
    {
        private readonly IMemoryStoreClientManager<IMemoryStoreClient> _memoryStoreClientManager;
        private readonly AnalyticsSenderClassWrapper _analytics;

        public InviteServiceImpl(IMemoryStoreClientManager<IMemoryStoreClient> memoryStoreClientManager,
            IAnalyticsSender analytics = null)
        {
            _memoryStoreClientManager = memoryStoreClientManager;
            _analytics = (analytics ?? new NullAnalyticsSender()).WithEventClass("party");
        }

        public override async Task<CreateInviteResponse> CreateInvite(CreateInviteRequest request,
            ServerCallContext context)
        {
            var playerId = AuthHeaders.ExtractPlayerId(context);

            if (string.IsNullOrEmpty(request.ReceiverPlayerId))
            {
                throw new RpcException(
                    new Status(StatusCode.InvalidArgument, "Expected a non-empty receiver player id"));
            }

            using (var memClient = _memoryStoreClientManager.GetClient())
            {
                var party = await GetPartyByPlayerId(memClient, playerId);
                // This extra check is necessary because the player might have meanwhile left the party (between the
                // Get<Member> and Get<PartyDataModel> calls).
                if (party?.GetMember(playerId) == null)
                {
                    throw new RpcException(new Status(StatusCode.FailedPrecondition,
                        "The player creating this invite is not a member of any party"));
                }

                if (party.GetMember(request.ReceiverPlayerId) != null)
                {
                    throw new RpcException(new Status(StatusCode.FailedPrecondition,
                        "The receiving player is already a member of the party"));
                }

                var entitiesToCreate = new List<Entry>();
                var entitiesToUpdate = new List<Entry>();

                var invite = new InviteDataModel(playerId, request.ReceiverPlayerId, party.Id, request.Metadata);
                entitiesToCreate.Add(invite);

                var senderPlayerInvites = await memClient.GetAsync<PlayerInvites>(playerId);
                if (senderPlayerInvites == null)
                {
                    senderPlayerInvites = new PlayerInvites(playerId);
                    entitiesToCreate.Add(senderPlayerInvites);
                }
                else
                {
                    entitiesToUpdate.Add(senderPlayerInvites);
                }

                senderPlayerInvites.OutboundInviteIds.Add(invite.Id);

                var receiverPlayerInvites = await memClient.GetAsync<PlayerInvites>(request.ReceiverPlayerId);
                if (receiverPlayerInvites == null)
                {
                    receiverPlayerInvites = new PlayerInvites(request.ReceiverPlayerId);
                    entitiesToCreate.Add(receiverPlayerInvites);
                }
                else
                {
                    entitiesToUpdate.Add(receiverPlayerInvites);
                }

                receiverPlayerInvites.InboundInviteIds.Add(invite.Id);

                using (var transaction = memClient.CreateTransaction())
                {
                    transaction.CreateAll(entitiesToCreate);
                    transaction.UpdateAll(entitiesToUpdate);
                }

                _analytics.Send("player_invited_to_party", new Dictionary<string, string>
                {
                    { "partyId", party.Id },
                    { "playerIdInviter", playerId },
                    { "inviteId", invite.Id }
                }, invite.ReceiverId);

                return new CreateInviteResponse { InviteId = invite.Id };
            }
        }

        public override async Task<DeleteInviteResponse> DeleteInvite(DeleteInviteRequest request,
            ServerCallContext context)
        {
            var playerId = AuthHeaders.ExtractPlayerId(context);

            if (string.IsNullOrEmpty(request.InviteId))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Expected non-empty invite id"));
            }

            using (var memClient = _memoryStoreClientManager.GetClient())
            {
                var invite = await memClient.GetAsync<InviteDataModel>(request.InviteId);
                if (invite == null)
                {
                    return new DeleteInviteResponse();
                }

                if (!invite.PlayerInvolved(playerId))
                {
                    throw new RpcException(new Status(StatusCode.PermissionDenied,
                        "The player is not involved in this invite"));
                }

                var senderInvites = await memClient.GetAsync<PlayerInvites>(invite.SenderId) ??
                                    throw new EntryNotFoundException(playerId, "No invites found for the sender");
                senderInvites.OutboundInviteIds.Remove(invite.Id);

                var receiverInvites = await memClient.GetAsync<PlayerInvites>(invite.ReceiverId) ??
                                      throw new EntryNotFoundException(playerId, "No invites found for the receiver");
                receiverInvites.InboundInviteIds.Remove(invite.Id);

                using (var transaction = memClient.CreateTransaction())
                {
                    transaction.DeleteAll(new List<InviteDataModel> { invite });
                    transaction.UpdateAll(new List<PlayerInvites> { senderInvites, receiverInvites });
                }

                _analytics.Send("player_invite_to_party_revoked", new Dictionary<string, string>
                {
                    { "partyId", invite.PartyId },
                    { "playerIdInviter", playerId },
                    { "inviteId", invite.Id }
                }, invite.ReceiverId);
            }

            return new DeleteInviteResponse();
        }

        // Updates the metadata and current status. Sender, receiver and party id are ignored.
        // TODO: consider moving to FieldMasks.
        public override async Task<UpdateInviteResponse> UpdateInvite(UpdateInviteRequest request,
            ServerCallContext context)
        {
            var playerId = AuthHeaders.ExtractPlayerId(context);

            var updatedInvite = request.UpdatedInvite ??
                                throw new RpcException(new Status(StatusCode.InvalidArgument,
                                    "Expected non-empty updated invite"));

            if (string.IsNullOrEmpty(updatedInvite.Id))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument,
                    "Expected updated invite with non-empty id"));
            }

            using (var memClient = _memoryStoreClientManager.GetClient())
            {
                var invite = await memClient.GetAsync<InviteDataModel>(updatedInvite.Id) ??
                             throw new EntryNotFoundException(updatedInvite.Id,
                                 "No such invite with the given id found");

                if (!invite.PlayerInvolved(playerId))
                {
                    throw new RpcException(new Status(StatusCode.PermissionDenied,
                        "The player is not involved in this invite"));
                }

                invite.CurrentStatus = ConvertToDataModel(updatedInvite.CurrentStatus);
                invite.UpdateMetadata(updatedInvite.Metadata);

                using (var transaction = memClient.CreateTransaction())
                {
                    transaction.UpdateAll(new List<Entry> { invite });
                }

                return new UpdateInviteResponse { Invite = ConvertToProto(invite) };
            }
        }

        public override async Task<GetInviteResponse> GetInvite(GetInviteRequest request, ServerCallContext context)
        {
            var playerId = AuthHeaders.ExtractPlayerId(context);

            if (string.IsNullOrEmpty(request.InviteId))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Expected non-empty invite id"));
            }

            using (var memClient = _memoryStoreClientManager.GetClient())
            {
                var invite = await memClient.GetAsync<InviteDataModel>(request.InviteId) ??
                             throw new EntryNotFoundException(request.InviteId,
                                 "No such invite with the given id found");

                if (!invite.PlayerInvolved(playerId))
                {
                    throw new RpcException(new Status(StatusCode.PermissionDenied,
                        "The player is not involved in this invite"));
                }

                return new GetInviteResponse { Invite = ConvertToProto(invite) };
            }
        }

        public override async Task<ListAllInvitesResponse> ListAllInvites(ListAllInvitesRequest request,
            ServerCallContext context)
        {
            var playerId = AuthHeaders.ExtractPlayerId(context);

            using (var memClient = _memoryStoreClientManager.GetClient())
            {
                var playerInvites = await memClient.GetAsync<PlayerInvites>(playerId);
                if (playerInvites == null)
                {
                    return new ListAllInvitesResponse();
                }

                var response = new ListAllInvitesResponse();
                foreach (var id in playerInvites.OutboundInviteIds)
                {
                    var invite = await memClient.GetAsync<InviteDataModel>(id) ??
                                 throw new RpcException(new Status(StatusCode.Unavailable,
                                     "Concurrent modification. Safe to retry"));
                    response.OutboundInvites.Add(ConvertToProto(invite));
                }

                foreach (var id in playerInvites.InboundInviteIds)
                {
                    var invite = await memClient.GetAsync<InviteDataModel>(id) ??
                                 throw new RpcException(new Status(StatusCode.Unavailable,
                                     "Concurrent modification. Safe to retry"));
                    response.InboundInvites.Add(ConvertToProto(invite));
                }

                return response;
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

        private static InviteProto ConvertToProto(InviteDataModel invite)
        {
            return new InviteProto
            {
                Id = invite.Id,
                SenderPlayerId = invite.SenderId,
                ReceiverPlayerId = invite.ReceiverId,
                PartyId = invite.PartyId,
                Metadata = { invite.Metadata },
                CurrentStatus = ConvertToProto(invite.CurrentStatus)
            };
        }

        private static InviteStatusDataModel ConvertToDataModel(InviteStatusProto phase)
        {
            switch (phase)
            {
                case InviteStatusProto.Pending:
                    return InviteStatusDataModel.Pending;
                case InviteStatusProto.Accepted:
                    return InviteStatusDataModel.Accepted;
                case InviteStatusProto.Declined:
                    return InviteStatusDataModel.Declined;
                default:
                    return InviteStatusDataModel.Unknown;
            }
        }

        private static InviteStatusProto ConvertToProto(InviteStatusDataModel status)
        {
            switch (status)
            {
                case InviteStatusDataModel.Pending:
                    return InviteStatusProto.Pending;
                case InviteStatusDataModel.Accepted:
                    return InviteStatusProto.Accepted;
                case InviteStatusDataModel.Declined:
                    return InviteStatusProto.Declined;
                default:
                    return InviteStatusProto.Unknown;
            }
        }
    }
}
