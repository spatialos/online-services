using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Improbable.SpatialOS.PlayerAuth.V2Alpha1;
using StackExchange.Redis;

namespace Improbable.OnlineServices.Common.Interceptors
{
    public class PlayerIdentityTokenValidatingInterceptor : Interceptor
    {
        public const string PlayerIdentityTokenHeaderKey = "x-player-identity-token";
        private readonly PlayerAuthServiceClient _authClient;
        private readonly IDatabase _cacheClient;
        private readonly HashAlgorithm _hashAlgorithm;
        private readonly TimeSpan _defaultCacheExpiry = TimeSpan.FromHours(1);

        public PlayerIdentityTokenValidatingInterceptor(PlayerAuthServiceClient authClient)
        {
            _authClient = authClient;
        }

        public PlayerIdentityTokenValidatingInterceptor(PlayerAuthServiceClient authClient, IDatabase cacheClient)
        {
            _authClient = authClient;
            _cacheClient = cacheClient;
            _hashAlgorithm = new SHA256Managed();
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
            var decodedPit = getFromCache(pit);
            if (decodedPit != null)
            {
                return decodedPit;
            }

            try
            {
                var resp = _authClient.DecodePlayerIdentityToken(new DecodePlayerIdentityTokenRequest
                {
                    PlayerIdentityToken = pit
                });
                cacheResult(pit, resp.PlayerIdentityToken);
                return resp.PlayerIdentityToken;
            }
            catch (Exception exception)
            {
                throw new RpcException(new Status(StatusCode.PermissionDenied,
                    "Failed to decode Player Identity Token: " + exception.Message));
            }
        }

        private PlayerIdentityToken getFromCache(string pit)
        {
            if (_cacheClient == null)
            {
                return null;
            }

            try
            {
                var key = getCacheKey(pit);
                var jsonPit = _cacheClient.StringGet(key);
                return JsonParser.Default.Parse<PlayerIdentityToken>(jsonPit);
            }
            catch (Exception e)
            {
                // TODO: log this but don't block on a broken cache
                return null;
            }
        }

        private void cacheResult(string pit, PlayerIdentityToken decodedPit)
        {
            if (_cacheClient == null)
            {
                return;
            }
            var offset = DateTimeOffset.FromUnixTimeSeconds(decodedPit.ExpiryTime.Seconds);
            var expiryFromNow = offset.Subtract(DateTime.Now);
            var cacheExpiry = _defaultCacheExpiry;
            if (expiryFromNow < cacheExpiry)
            {
                cacheExpiry = expiryFromNow;
            }

            try
            {
                var jsonPit = JsonFormatter.Default.Format(decodedPit);
                var key = getCacheKey(pit);
                _cacheClient.StringSet(key, jsonPit, cacheExpiry);
            }
            catch
            {
                // TODO: log this but don't block on a broken cache
            }
        }

        private string getCacheKey(string pit)
        {
            var stringBytes = Encoding.UTF8.GetBytes(pit);
            var hash = _hashAlgorithm.ComputeHash(stringBytes);
            return Convert.ToBase64String(hash);
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