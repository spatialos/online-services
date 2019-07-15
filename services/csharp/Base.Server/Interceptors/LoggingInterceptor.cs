using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Improbable.MetagameServices.Base.Server.Logging;

namespace Improbable.MetagameServices.Base.Server.Interceptors
{
    public class LoggingInterceptor : Interceptor
    {
        private static readonly ILog Logger = LogProvider.GetCurrentClassLogger();

        public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(TRequest request, ServerCallContext context,
            UnaryServerMethod<TRequest, TResponse> continuation)
        {
            try
            {
                var startTime = DateTime.UtcNow;
                var watch = new Stopwatch();
                watch.Start();
                using (LogProvider.OpenMappedContext("grpcMethod", context.Method))
                using (LogProvider.OpenMappedContext("peer", context.Peer))
                using (LogProvider.OpenMappedContext("grpcStartTime", startTime.ToString("u")))
                {
                    var response = await continuation.Invoke(request, context);
                    watch.Stop();
                    var elapsedMs = watch.ElapsedMilliseconds;
                    Logger.Info("{peer} {grpcMethod} {grpcCode} {grpcTimeMs}", context.Peer, context.Method, context.Status.StatusCode, elapsedMs);
                    return response;
                }
            }
            catch (Exception e)
            {
                Logger.Error($"Got exception in RPC {context.Method}: {e.Message}\n{e.StackTrace}");
                throw;
            }
        }
    }
}
