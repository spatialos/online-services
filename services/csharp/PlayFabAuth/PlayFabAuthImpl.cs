using Improbable.OnlineServices.Proto.Auth.PlayFab;
using Grpc.Core;
using PlayFab;
using PlayFab.ServerModels;
using System.Threading.Tasks;
using System;
using Serilog;
using Improbable.SpatialOS.PlayerAuth.V2Alpha1;

namespace PlayFabAuth
{
    public class PlayFabAuthImpl : AuthService.AuthServiceBase
    {
        private readonly string _project;
        private readonly PlayerAuthServiceClient _authServiceClient;

        public PlayFabAuthImpl(string project, PlayerAuthServiceClient authServiceClient)
        {
            _project = project;
            _authServiceClient = authServiceClient;
        }

        public override Task<ExchangePlayFabTokenResponse> ExchangePlayFabToken(ExchangePlayFabTokenRequest request,
            ServerCallContext context)
        {
            UserAccountInfo userInfo;

            try
            {
                var authenticateTask = PlayFabServerAPI.AuthenticateSessionTicketAsync(
                    new AuthenticateSessionTicketRequest
                    {
                        SessionTicket = request.PlayfabToken,
                    });
                authenticateTask.Wait();
                userInfo = authenticateTask.GetAwaiter().GetResult().Result.UserInfo;
            }
            catch (Exception e)
            {
                Log.Error(e, "Failed to authenticate PlayFab ticket");
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Failed to authenticate PlayFab ticket"));
            }

            try
            {
                var playerIdentityToken = _authServiceClient.CreatePlayerIdentityToken(
                    new CreatePlayerIdentityTokenRequest
                    {
                        PlayerIdentifier = userInfo.PlayFabId,
                        Provider = "playfab",
                        ProjectName = _project
                    }
                );
                return Task.FromResult(new ExchangePlayFabTokenResponse
                    {PlayerIdentityToken = playerIdentityToken.PlayerIdentityToken});
            }
            catch (Exception e)
            {
                Log.Error(e, $"Failed to create player identity token for {userInfo.PlayFabId}");
                throw new RpcException(new Status(StatusCode.Internal,
                    $"Failed to create player identity token for {userInfo.PlayFabId}"));
            }
        }
    }
}