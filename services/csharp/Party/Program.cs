using System;
using System.Collections.Generic;
using System.Threading;
using CommandLine;
using Grpc.Core;
using Improbable.OnlineServices.Base.Server;
using Improbable.OnlineServices.Base.Server.Interceptors;
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
        public static void Main(string[] args)
        {
            // Required to have enough I/O threads to handle Redis+gRPC traffic
            // See https://support.microsoft.com/en-gb/help/821268/contention-poor-performance-and-deadlocks-when-you-make-calls-to-web-s
            ThreadPool.SetMaxThreads(100, 100);
            ThreadPool.SetMinThreads(50, 50);
            
            Parser.Default.ParseArguments<PartyServerCommandLineArgs>(args)
                .WithParsed(parsedArgs =>
                {
                    VerifyArgs(parsedArgs);

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
                                PlayerAuthServiceClient.Create(credentials: new PlatformRefreshTokenCredential(parsedArgs.RefreshToken)),
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
                        server.Start();
                        Log.Information("Server started. Waiting for requests.");
                        UnixSignal.WaitAny(new[] {new UnixSignal(Signum.SIGINT), new UnixSignal(Signum.SIGTERM)});
                    }
                });
        }

        private static void VerifyArgs(PartyServerCommandLineArgs args)
        {
            if (args.DefaultMinMembers < 0)
            {
                throw new ArgumentException("DefaultMinMembers cannot be negative");
            }

            if (args.DefaultMaxMembers < 0)
            {
                throw new ArgumentException("DefaultMinMembers cannot be negative");
            }
        }
    }
}