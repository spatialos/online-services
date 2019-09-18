using System;
using System.Linq;
using System.Threading;
using CommandLine;
using Google.LongRunning;
using Grpc.Core;
using Improbable.OnlineServices.Proto.Auth.PlayFab;
using Improbable.OnlineServices.Proto.Gateway;
using Improbable.OnlineServices.Proto.Party;
using PlayFab;
using PlayFab.ClientModels;

namespace SampleClient
{

    public class SampleClientArguments
    {
        [Option("google_project", HelpText = "Google project ID", Required = true)]
        public string GoogleProject { get; set; }

        [Option("playfab_title_id", HelpText = "PlayFab title ID", Required = true)]
        public string PlayFabTitleId { get; set; }

        [Option("local", HelpText = "Connects the client to services deployed locally")]
        public bool Local { get; set; }
    }

    /**
     * SampleClient is used in conjunction with the quickstart guide to demo the matchmaking system.
     */
    class Program
    {
        private const string LocalEndPointUrlFormat = "localhost:{0}";
        private const string CloudEndPointUrlFormat = "{0}.endpoints.{1}.cloud.goog:4000";
        private const string PitRequestHeaderName = "player-identity-token";

        static void Main(string[] args)
        {

            Parser.Default.ParseArguments<SampleClientArguments>(args)
                .WithParsed(parsedArgs =>
                {
                    var gatewayServiceUrl = parsedArgs.Local
                        ? string.Format(LocalEndPointUrlFormat, "4040")
                        : string.Format(CloudEndPointUrlFormat, "gateway", parsedArgs.GoogleProject);

                    var partyServiceUrl = parsedArgs.Local
                        ? string.Format(LocalEndPointUrlFormat, "4041")
                        : string.Format(CloudEndPointUrlFormat, "party", parsedArgs.GoogleProject);

                    var authServiceUrl = parsedArgs.Local
                        ? string.Format(LocalEndPointUrlFormat, "4042")
                        : string.Format(CloudEndPointUrlFormat, "playfab-auth", parsedArgs.GoogleProject);

                    var playerId = RandomString(15);
                    Console.WriteLine($"Using a randomly generated PlayFab player ID: {playerId}");

                    // First, get a token from PlayFab.
                    PlayFabSettings.staticSettings.TitleId = parsedArgs.PlayFabTitleId;
                    var playFabLoginTask = PlayFabClientAPI.LoginWithCustomIDAsync(new LoginWithCustomIDRequest
                    {
                        TitleId = parsedArgs.PlayFabTitleId,
                        CustomId = playerId,
                        CreateAccount = true
                    });
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
                        new Channel(authServiceUrl, ChannelCredentials.Insecure));
                    var authResult = playFabAuthClient.ExchangePlayFabToken(new ExchangePlayFabTokenRequest
                    {
                        PlayfabToken = playFabLoginResult.Result.SessionTicket
                    });
                    Console.WriteLine("Got a PIT.");
                    var pitMetadata = new Metadata { { PitRequestHeaderName, authResult.PlayerIdentityToken } };

                    // Create a single-player party for the player.
                    var partyClient = new PartyService.PartyServiceClient(
                        new Channel(partyServiceUrl, ChannelCredentials.Insecure));
                    var partyResponse =
                        partyClient.CreateParty(new CreatePartyRequest { MinMembers = 1, MaxMembers = 1 }, pitMetadata);
                    Console.WriteLine($"Created a new party with id {partyResponse.PartyId}.");

                    var gatewayEndpoint = gatewayServiceUrl;
                    var gatewayClient =
                        new GatewayService.GatewayServiceClient(new Channel(gatewayEndpoint,
                            ChannelCredentials.Insecure));
                    var operationsClient =
                        new Operations.OperationsClient(new Channel(gatewayEndpoint, ChannelCredentials.Insecure));

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
                    Console.WriteLine(
                        $"Got deployment: {response.DeploymentName}. Login token: [{response.LoginToken}].");
                });
        }

        private static string RandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[new Random().Next(s.Length)]).ToArray());
        }
    }
}
