using System;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using Improbable.OnlineServices.Base.Server;
using Improbable.OnlineServices.Common;
using Improbable.OnlineServices.Common.Analytics;
using Improbable.OnlineServices.Common.Analytics.ExceptionHandlers;
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
    public class PlayFabAuthArguments : CommandLineArgs, IAnalyticsCommandLineArgs
    {
        [Option("spatial_project", HelpText = "Spatial project name", Required = true)]
        public string SpatialProject { get; set; }

        [Option("playfab_title_id", HelpText = "PlayFab title ID", Required = true)]
        public string PlayFabTitleId { get; set; }

        public string Endpoint { get; set; }
        public bool AllowInsecureEndpoints { get; set; }
        public string ConfigPath { get; set; }
        public string GcpKeyPath { get; set; }
        public string Environment { get; set; }
    }

    public class Program
    {
        private const string SpatialRefreshTokenEnvironmentVariable = "SPATIAL_REFRESH_TOKEN";
        private const string PlayFabSecretKeyEnvironmentVariable = "PLAYFAB_SECRET_KEY";

        public static void Main(string[] args)
        {
            // See https://support.microsoft.com/en-gb/help/821268/contention-poor-performance-and-deadlocks-when-you-make-calls-to-web-s
            // Experimentation shows we need the ThreadPool to always spin up threads for good performance under load
            ThreadPool.GetMaxThreads(out var workerThreads, out var ioThreads);
            ThreadPool.SetMinThreads(workerThreads, ioThreads);

            Parser.Default.ParseArguments<PlayFabAuthArguments>(args)
                .WithParsed(parsedArgs =>
                {
                    Log.Logger = new LoggerConfiguration()
                        .WriteTo.Console(new RenderedCompactJsonFormatter())
                        .Enrich.FromLogContext()
                        .CreateLogger();

                    var spatialRefreshToken = Secrets.GetEnvSecret(SpatialRefreshTokenEnvironmentVariable).Trim();
                    var playfabDeveloperKey = Secrets.GetEnvSecret(PlayFabSecretKeyEnvironmentVariable).Trim();

                    PlayFabSettings.DeveloperSecretKey = playfabDeveloperKey;
                    PlayFabSettings.TitleId = parsedArgs.PlayFabTitleId;
                    
                    IAnalyticsSender analyticsSender = new AnalyticsSenderBuilder("playfab_auth")
                        .WithCommandLineArgs(parsedArgs)
                        .With(new LogExceptionStrategy(Log.Logger))
                        .Build();

                    var server = GrpcBaseServer.Build(parsedArgs);
                    server.AddService(AuthService.BindService(
                        new PlayFabAuthImpl(
                            parsedArgs.SpatialProject,
                            PlayerAuthServiceClient.Create(
                                credentials: new PlatformRefreshTokenCredential(spatialRefreshToken)),
                            analyticsSender)
                    ));
                    var serverTask = Task.Run(() => server.Start());
                    var signalTask = Task.Run(() => UnixSignal.WaitAny(new[] { new UnixSignal(Signum.SIGINT), new UnixSignal(Signum.SIGTERM) }));
                    Log.Information("PlayFab authentication server started up");
                    Task.WaitAny(serverTask, signalTask);

                    if (signalTask.IsCompleted)
                    {
                        Log.Information($"Received UNIX signal {signalTask.Result}");
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
