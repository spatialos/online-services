using System;
using System.Collections.Generic;
using System.IO;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Improbable.OnlineServices.Base.Server.Interceptors;
using PrometheusServerInterceptor = NetGrpcPrometheus.ServerInterceptor;
using Improbable.OnlineServices.Base.Server.Logging;

namespace Improbable.OnlineServices.Base.Server
{
    public class GrpcBaseServer : IDisposable
    {
        private static readonly ILog Logger = LogProvider.GetCurrentClassLogger();

        private readonly Grpc.Core.Server _server;
        private readonly List<Interceptor> _interceptorChain = new List<Interceptor>();

        public static GrpcBaseServer Build(CommandLineArgs args)
        {
            Logger.Info(args.ToString);
            return new GrpcBaseServer(
                args.HostName, args.SslCertChainPath, args.SslPrivateKeyPath,
                args.GrpcServicePort, args.MetricsPort, args.DisablePrometheus
            );
        }

        private GrpcBaseServer(
            string host, string sslCertChainPath, string sslPrivateKeyPath,
            int port, int metricsPort, bool disablePrometheusInterceptor
        )
        {
            var credentials = ServerCredentials.Insecure;
            if (string.IsNullOrEmpty(sslCertChainPath) || string.IsNullOrEmpty(sslPrivateKeyPath))
            {
                Logger.Warn($"SSL certificate chain and/or private key paths were not provided. " +
                            "Server will start in insecure non-TLS mode.");
            }
            else
            {
                credentials = new SslServerCredentials(new[]
                {
                    new KeyCertificatePair(File.ReadAllText(sslCertChainPath), File.ReadAllText(sslPrivateKeyPath))
                });
            }

            // Build a server
            _server = new Grpc.Core.Server
            {
                Ports = { new ServerPort(host, port, credentials) }
            };

            if (!disablePrometheusInterceptor)
            {
                try
                {
                    _interceptorChain.Add(new PrometheusServerInterceptor("*", metricsPort, true, true));
                }
                catch (Exception e)
                {
                    Console.WriteLine("Exception creating prometheus interceptor: " + e);
                }
            }
        }

        public GrpcBaseServer AddInterceptor(Interceptor interceptor)
        {
            _interceptorChain.Add(interceptor);
            return this;
        }

        public void AddService(ServerServiceDefinition serviceDefinition)
        {
            _server.Services.Add(_addInterceptors(serviceDefinition));
        }

        public void Start()
        {
            Logger.Info("Starting Server...");
            _server.Start();
        }

        public void Shutdown()
        {
            Logger.Info("Shutdown server...");
            _server.ShutdownAsync().Wait();
            Logger.Info("Shutdown cleanly.");
        }

        private ServerServiceDefinition _addInterceptors(ServerServiceDefinition service)
        {
            foreach (var interceptor in _interceptorChain)
            {
                service = service.Intercept(interceptor);
            }

            return service
                .Intercept(new LoggingInterceptor());
        }

        public void Dispose()
        {
            try
            {
                Shutdown();
            }
            catch (Exception ex)
            {
                Logger.Warn("Exception while shutting down: " + ex);
            }
        }
    }
}