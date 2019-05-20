using System;
using Grpc.Core;
using Improbable.OnlineServices.Proto.Auth.PlayFab;
using Improbable.OnlineServices.Proto.Party;
using Improbable.SpatialOS.Platform.Common;
using Improbable.SpatialOS.PlayerAuth.V2Alpha1;
using Improbable.SpatialOS.ServiceAccount.V1Alpha1;
using NUnit.Framework;
using PlayFab;
using PlayFab.ClientModels;

namespace IntegrationTest
{
    [TestFixture]
    public class PlayFabAuthShould
    {
        private const string AuthServerTarget = "127.0.0.1:4042";
        private const string PlayfabTitleId = "D6DE8";
        private const string PlayfabPlayerId = "integration_test_player";

        private static AuthService.AuthServiceClient _authServiceClient;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            var channel = new Channel(AuthServerTarget, ChannelCredentials.Insecure);
            _authServiceClient = new AuthService.AuthServiceClient(channel);
            PlayFabSettings.TitleId = PlayfabTitleId;
        }

        [Test]
        public void ReturnInvalidArgumentErrorIfPlayFabTokenNotProvided()
        {
            var exception = Assert.Throws<RpcException>(() =>
                _authServiceClient.ExchangePlayFabToken(new ExchangePlayFabTokenRequest()));
            Assert.AreEqual(StatusCode.InvalidArgument, exception.StatusCode);
        }

        [Test]
        public void AuthenticatePlayerWithValidPlayfabSessionTicket()
        {
            var ticket = GetPlayerSessionTicket();

            var authServiceReq = new ExchangePlayFabTokenRequest {PlayfabToken = ticket};

            var authResult = _authServiceClient.ExchangePlayFabToken(authServiceReq);
            Assert.NotNull(authResult.PlayerIdentityToken);
        }

        private static string GetPlayerSessionTicket()
        {
            // Login a new custom player
            var loginRequest = new LoginWithCustomIDRequest
            {
                CreateAccount = true,
                CustomId = PlayfabPlayerId
            };

            var loginTask = PlayFabClientAPI.LoginWithCustomIDAsync(loginRequest);
            loginTask.Wait();
            var loginResult = loginTask.GetAwaiter().GetResult();
            Assert.Null(loginResult.Error);

            Assert.NotNull(loginResult.Result.SessionTicket);
            return loginResult.Result.SessionTicket; 
        }
    }
}

