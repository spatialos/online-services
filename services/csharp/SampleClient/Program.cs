using System;
using System.Linq;
using System.Threading;
using Google.LongRunning;
using Grpc.Core;
using Improbable.OnlineServices.Proto.Auth.PlayFab;
using Improbable.OnlineServices.Proto.Gateway;
using Improbable.OnlineServices.Proto.Party;
using PlayFab;
using PlayFab.ClientModels;

namespace SampleClient
{
    class Program
    {
        private const string EndPointUrlFormat = "{0}.endpoints.{1}.cloud.goog:4000";
        private const string PitRequestHeaderName = "player-identity-token";

        static void Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.WriteLine("Google project and PlayFab title ID args are required.");
                Environment.Exit(1);
                return;
            }

            var project = args[0];
            var titleId = args[1];
            var playerId = RandomString(15);
            Console.WriteLine($"Using a randomly generated PlayFab player ID: {playerId}");

            // First, get a token from PlayFab.
            PlayFabSettings.staticSettings.TitleId = titleId;
            var playFabLoginTask = PlayFabClientAPI.LoginWithCustomIDAsync(new LoginWithCustomIDRequest
            {
                TitleId = titleId,
                CustomId = playerId,
                CreateAccount = true
            });
            playFabLoginTask.Wait();
            var playFabLoginResult = playFabLoginTask.GetAwaiter().GetResult();
            if (playFabLoginResult.Error != null)
            {
                Console.WriteLine($"Got login error from PlayFab: {playFabLoginResult.Error.ErrorMessage}");
                Environment.Exit(1);
                return;
            }

            var playFabId = playFabLoginResult.Result.PlayFabId;

            Console.WriteLine($"Got a token for PlayFab ID {playFabId}.");

            // Next, exchange the token with our auth service for a PIT.
            var playFabAuthClient = new AuthService.AuthServiceClient(
                new Channel(string.Format(EndPointUrlFormat, "playfab-auth", project), ChannelCredentials.Insecure));
            var authResult = playFabAuthClient.ExchangePlayFabToken(new ExchangePlayFabTokenRequest
            {
                PlayfabToken = playFabLoginResult.Result.SessionTicket
            });
            Console.WriteLine("Got a PIT.");
            var pitMetadata = new Metadata { { PitRequestHeaderName, authResult.PlayerIdentityToken } };

            // Create a single-player party for the player.
            var partyClient = new PartyService.PartyServiceClient(
                new Channel(string.Format(EndPointUrlFormat, "party", project), ChannelCredentials.Insecure));
            var partyResponse =
                partyClient.CreateParty(new CreatePartyRequest { MinMembers = 1, MaxMembers = 1 }, pitMetadata);
            Console.WriteLine($"Created a new party with id {partyResponse.PartyId}.");

            var gatewayClient = new GatewayService.GatewayServiceClient(
                new Channel(string.Format(EndPointUrlFormat, "gateway", project), ChannelCredentials.Insecure));
            var operationsClient = new Operations.OperationsClient(
                new Channel(string.Format(EndPointUrlFormat, "gateway", project), ChannelCredentials.Insecure));

            var op = gatewayClient.Join(new JoinRequest
            {
                MatchmakingType = "match"
            }, pitMetadata);
            Console.WriteLine("Joined queue; waiting for match.");

            while (!op.Done)
            {
                Thread.Sleep(1000);
                op = operationsClient.GetOperation(new GetOperationRequest
                {
                    Name = op.Name
                }, pitMetadata);
            }

            var response = op.Response.Unpack<JoinResponse>();
            Console.WriteLine($"Got deployment: {response.DeploymentName}. Login token: [{response.LoginToken}].");
        }

        private static string RandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[new Random().Next(s.Length)]).ToArray());
        }
    }
}