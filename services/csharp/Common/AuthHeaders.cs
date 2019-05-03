using System.Linq;
using Grpc.Core;
using Improbable.OnlineServices.Common.Interceptors;

namespace Improbable.OnlineServices.Common
{
    public static class AuthHeaders
    {
        private const string AuthenticatedHeader = "x-internal-is-authenticated";
        private const string PlayerIdentifierHeader = "x-internal-player-identifier";

        public static void AddAuthenticatedHeaderToContext(ServerCallContext context, bool authenticated)
        {
            context.RequestHeaders.Add(AuthenticatedHeader, authenticated ? "true" : "");
        }

        public static void AddPlayerIdentifierToContext(ServerCallContext context, string playerIdentifier)
        {
            context.RequestHeaders.Add(PlayerIdentifierHeader, playerIdentifier);
        }

        public static void CheckRequestAuthenticated(ServerCallContext context)
        {
            if (!"true".Equals(
                context.RequestHeaders.SingleOrDefault(item => item.Key.Equals(AuthenticatedHeader))?.Value
            ))
            {
                throw new RpcException(new Status(StatusCode.PermissionDenied, "incorrect secret provided in request"));
            }
        }

        public static string ExtractPlayerId(ServerCallContext context)
        {
            return context.RequestHeaders.SingleOrDefault(item => item.Key == PlayerIdentifierHeader)?.Value;
        }

        public static string ExtractPit(ServerCallContext context)
        {
            return context.RequestHeaders.SingleOrDefault(item =>
                item.Key == PlayerIdentityTokenValidatingInterceptor.PlayerIdentityTokenHeaderKey)?.Value;
        }

        public static bool ValidatePlayerId(string playerId)
        {
            return !string.IsNullOrEmpty(playerId);
        }

        public static bool ValidatePit(string pit)
        {
            return !string.IsNullOrEmpty(pit);
        }
    }
}