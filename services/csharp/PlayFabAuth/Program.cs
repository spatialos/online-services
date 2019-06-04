using System;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using Improbable.OnlineServices.Base.Server;
using Improbable.OnlineServices.Proto.Auth.PlayFab;
using Improbable.SpatialOS.Platform.Common;
using Improbable.SpatialOS.PlayerAuth.V2Alpha1;
using Mono.Unix;
using PlayFab;
using Mono.Unix.Native;
using Serilog;
using Serilog.Formatting.Compact;

namespace PlayFabAuth
{
    public class PlayFabAuthArguments : CommandLineArgs
    {
        [Option("spatial_project", HelpText = "Spatial project name", Required = true)]
        public string SpatialProject { get; set; }

        [Option("playfab_secret_key", HelpText = "PlayFab developer secret key", Required = true)]
        public string PlayFabSecretKey { get; set; }

        [Option("playfab_title_id", HelpText = "PlayFab title ID", Required = true)]
        public string PlayFabTitleId { get; set; }

        [Option("spatial_refresh_token", HelpText = "Refresh Token to communicate with SpatialOS", Required = true)]
        public string RefreshToken { get; set; }
    }

    public class Program
    {
        public static void Main(string[] args)
        {
            Parser.Default.ParseArguments<PlayFabAuthArguments>(args)
                .WithParsed(parsedArgs =>
                {
                    Log.Logger = new LoggerConfiguration()
                        .WriteTo.Console(new RenderedCompactJsonFormatter())
                        .Enrich.FromLogContext()
                        .CreateLogger();

                    PlayFabSettings.DeveloperSecretKey = parsedArgs.PlayFabSecretKey;
                    PlayFabSettings.TitleId = parsedArgs.PlayFabTitleId;

                    var server = GrpcBaseServer.Build(parsedArgs);
                    server.AddService(AuthService.BindService(
                        new PlayFabAuthImpl(
                            parsedArgs.SpatialProject,
                            PlayerAuthServiceClient.Create(
                                credentials: new PlatformRefreshTokenCredential(parsedArgs.RefreshToken))
                        )
                    ));
                    var unixSignalTask = new Task<int>(() =>
                        UnixSignal.WaitAny(new[] { new UnixSignal(Signum.SIGINT), new UnixSignal(Signum.SIGTERM) }));
                    var serverTask = new Task(() => server.Start());

                    Log.Information("PlayFab authentication server started up");
                    Task.WaitAny(serverTask, unixSignalTask);
                    if (unixSignalTask.IsCompleted)
                    {
                        Log.Information($"Received UNIX signal {unixSignalTask.Result}");
                        Log.Information("Server shutting down...");
                        server.Shutdown();
                        serverTask.Wait();
                        Log.Information("Server stopped cleanly");
                    }
                    else
                    {
                        /* The server task has completed; we can just exit. */
                        Log.Information("The PlayFab authentication server has stopped itself or encountered an unhandled exception.");
                    }

                    Environment.Exit(0);
                });
        }
    }
}
