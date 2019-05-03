using System;
using System.Linq;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Improbable.SpatialOS.PlayerAuth.V2Alpha1;

namespace Improbable.OnlineServices.Common.Interceptors
{
    public class PlayerIdentityTokenValidatingInterceptor : Interceptor
    {
        public const string PlayerIdentityTokenHeaderKey = "player_identity_token";
        private readonly PlayerAuthServiceClient _authClient;

        public PlayerIdentityTokenValidatingInterceptor(PlayerAuthServiceClient authClient)
        {
            _authClient = authClient;
        }

        public override Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
            TRequest request,
            ServerCallContext context,
            UnaryServerMethod<TRequest, TResponse> continuation)
        {
            var pit = ExtractPlayerIdentityTokenFromContext(context);
            var decodedPit = DecodePlayerIdentityToken(pit);
            var playerIdentifier = ExtractPlayerIdentifier(decodedPit);
            AuthHeaders.AddPlayerIdentifierToContext(context, playerIdentifier);

            return continuation(request, context);
        }

        private static string ExtractPlayerIdentityTokenFromContext(ServerCallContext context)
        {
            var pit = context.RequestHeaders.SingleOrDefault(item => item.Key == PlayerIdentityTokenHeaderKey)?.Value;
            if (!AuthHeaders.ValidatePit(pit))
            {
                throw new RpcException(new Status(StatusCode.PermissionDenied,
                    "The request header doesn't contain a Player Identity Token."));
            }

            return pit;
        }

        private PlayerIdentityToken DecodePlayerIdentityToken(string pit)
        {
            try
            {
                var resp = _authClient.DecodePlayerIdentityToken(new DecodePlayerIdentityTokenRequest
                {
                    PlayerIdentityToken = pit
                });
                return resp.PlayerIdentityToken;
            }
            catch (Exception exception)
            {
                throw new RpcException(new Status(StatusCode.PermissionDenied,
                    "Failed to decode Player Identity Token: " + exception.Message));
            }
        }

        private static string ExtractPlayerIdentifier(PlayerIdentityToken decodedPit)
        {
            var playerIdentifier = decodedPit.PlayerIdentifier;
            if (!AuthHeaders.ValidatePlayerId(playerIdentifier))
            {
                throw new RpcException(new Status(StatusCode.PermissionDenied,
                    "The Player Identity Token doesn't contain a valid player_identifier"));
            }

            return playerIdentifier;
        }
    }
}