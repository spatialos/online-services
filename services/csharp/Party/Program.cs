using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using Grpc.Core;
using Improbable.OnlineServices.Base.Server;
using Improbable.OnlineServices.Base.Server.Interceptors;
using Improbable.OnlineServices.Common;
using Improbable.OnlineServices.Common.Interceptors;
using Improbable.OnlineServices.Proto.Invite;
using Improbable.OnlineServices.Proto.Party;
using Improbable.SpatialOS.Platform.Common;
using Improbable.SpatialOS.PlayerAuth.V2Alpha1;
using MemoryStore;
using MemoryStore.Redis;
using Mono.Unix;
using Mono.Unix.Native;
using Serilog;
using Serilog.Formatting.Compact;
using PartyDataModel = Improbable.OnlineServices.DataModel.Party.Party;

namespace Party
{
    public class Program
    {
        private const string SpatialRefreshTokenEnvironmentVariable = "SPATIAL_REFRESH_TOKEN";

        public static void Main(string[] args)
        {
            // See https://support.microsoft.com/en-gb/help/821268/contention-poor-performance-and-deadlocks-when-you-make-calls-to-web-s
            // Experimentation shows we need the ThreadPool to always spin up threads for good performance under load
            ThreadPool.GetMaxThreads(out var workerThreads, out var ioThreads);
            ThreadPool.SetMinThreads(workerThreads, ioThreads);

            Parser.Default.ParseArguments<PartyServerCommandLineArgs>(args)
                .WithParsed(parsedArgs =>
                {
                    parsedArgs.Validate();

                    var spatialRefreshToken = Secrets.GetEnvSecret(SpatialRefreshTokenEnvironmentVariable);

                    PartyDataModel.Defaults.MinMembers = (uint) parsedArgs.DefaultMinMembers;
                    PartyDataModel.Defaults.MaxMembers = (uint) parsedArgs.DefaultMaxMembers;

                    Log.Logger = new LoggerConfiguration()
                        .WriteTo.Console(new RenderedCompactJsonFormatter())
                        .Enrich.FromLogContext()
                        .CreateLogger();

                    using (var server = GrpcBaseServer.Build(parsedArgs))
                    using (var memoryStoreManager = new RedisClientManager(parsedArgs.RedisConnectionString))
                    {
                        Log.Information($"Successfully connected to Redis at {parsedArgs.RedisConnectionString}");
                        server.AddInterceptor(new PlayerIdentityTokenValidatingInterceptor(
                                PlayerAuthServiceClient.Create(credentials: new PlatformRefreshTokenCredential(spatialRefreshToken)),
                                memoryStoreManager.GetRawClient(Database.CACHE)
                            ))
                            .AddInterceptor(new ExceptionMappingInterceptor(new Dictionary<Type, StatusCode>
                            {
                                {typeof(EntryNotFoundException), StatusCode.NotFound},
                                {typeof(EntryAlreadyExistsException), StatusCode.AlreadyExists},
                                {typeof(TransactionAbortedException), StatusCode.Unavailable}
                            }));
                        server.AddService(
                            PartyService.BindService(new PartyServiceImpl(memoryStoreManager)));
                        server.AddService(
                            InviteService.BindService(new InviteServiceImpl(memoryStoreManager)));
                        var serverTask = Task.Run(() => server.Start());
                        var signalTask = Task.Run(() => UnixSignal.WaitAny(new[] { new UnixSignal(Signum.SIGINT), new UnixSignal(Signum.SIGTERM) }));
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
                            Log.Information("The Party server has stopped itself or encountered an unhandled exception.");
                        }
                    }
                });
        }

    }
}
