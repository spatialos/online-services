using System;
using System.Collections.Generic;
using System.IO;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Improbable.OnlineServices.Base.Server.Interceptors;
using PrometheusServerInterceptor = NetGrpcPrometheus.ServerInterceptor;
using Improbable.OnlineServices.Base.Server.Logging;
using Improbable.OnlineServices.Common;
using Improbable.OnlineServices.Common.Interceptors;

namespace Improbable.OnlineServices.Base.Server
{
    public class GrpcBaseServer : IDisposable
    {
        private static readonly ILog _logger = LogProvider.GetCurrentClassLogger();

        // TODO: migrate to passing the backend host and secret through command line args
        private const string DefaultSecret = "RememberToChangeThisInProduction";
        private const string HostEnvVar = "IMPROBABLE_BACKEND_HOST";
        private const string SecretEnvVar = "IMPROBABLE_BACKEND_SECRET";
        private const string SslCertChainAndPubKeyPath = "IMPROBABLE_SSL_CERTCHAIN_AND_PUBKEY_PATH";
        private const string SslPrivateKeyPath = "IMPROBABLE_SSL_PRIVATE_KEY_PATH";

        private readonly string _secret;
        private readonly Grpc.Core.Server _server;
        private readonly List<Interceptor> _interceptorChain = new List<Interceptor>();

        public static GrpcBaseServer Build(CommandLineArgs args)
        {
            _logger.Info(args.ToString);
            return new GrpcBaseServer(args.GrpcServicePort, args.MetricsPort, args.DisablePrometheus);
        }

        private GrpcBaseServer(int port = 4040, int metricsPort = 9090, bool disablePrometheusInterceptor = false)
        {
            var defaults = new Dictionary<string, string> {[SecretEnvVar] = DefaultSecret, [HostEnvVar] = "0.0.0.0"};
            var secretProvider = new EnvironmentVarSecretProvider(defaults);

            var host = secretProvider[HostEnvVar] ?? "0.0.0.0";
            ServerCredentials credentials = ServerCredentials.Insecure;
            
            try
            {
                string certChainAndPubKey = File.ReadAllText(secretProvider[SslCertChainAndPubKeyPath]);
                string privateKey = File.ReadAllText(secretProvider[SslPrivateKeyPath]);
                credentials = new SslServerCredentials(new KeyCertificatePair[]
                    {new KeyCertificatePair(certChainAndPubKey, privateKey)});
            }
            catch (KeyNotFoundException)
            {
                _logger.Warn($"One of {SslCertChainAndPubKeyPath } and {SslPrivateKeyPath} is not set. " +
                             "Server will start in insecure non-TLS mode.");
            }
            
            // Build a server
            _server = new Grpc.Core.Server
            {
                Ports = {new ServerPort(host, port, credentials)}
            };

            _secret = secretProvider[SecretEnvVar];

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
            _logger.Info("Starting Server...");
            _server.Start();
        }

        public void Shutdown()
        {
            _logger.Info("Shutdown server...");
            _server.ShutdownAsync().Wait();
            _logger.Info("Shutdown cleanly.");
        }

        private ServerServiceDefinition _addInterceptors(ServerServiceDefinition service)
        {
            foreach (var interceptor in _interceptorChain)
            {
                service = service.Intercept(interceptor);
            }

            return service
                .Intercept(new SecretCheckingInterceptor(_secret))
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
                _logger.Warn("Exception while shutting down: " + ex);
            }
        }
    }
}