using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Improbable.OnlineServices.Base.Server.Logging;

namespace Improbable.OnlineServices.Base.Server.Interceptors
{
    public class ExceptionMappingInterceptor : Interceptor
    {
        private static readonly ILog _logger = LogProvider.GetCurrentClassLogger();
        private readonly IDictionary<Type, StatusCode> _mapping;

        public ExceptionMappingInterceptor(IDictionary<Type, StatusCode> mapping)
        {
            _mapping = mapping ?? new Dictionary<Type, StatusCode>();
        }

        public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(TRequest request,
            ServerCallContext context,
            UnaryServerMethod<TRequest, TResponse> continuation)
        {
            try
            {
                return await continuation.Invoke(request, context);
            }
            catch (RpcException)
            {
                throw;
            }
            catch (Exception exception)
            {
                var exceptionType = exception.GetType();
                if (_mapping.ContainsKey(exceptionType))
                {
                    throw new RpcException(new Status(_mapping[exceptionType], exception.Message));
                }

                _logger.Warn($"Caught unmapped exception: {exception}");
                throw new RpcException(new Status(StatusCode.Unknown, "Internal Server Error"));
            }
        }
    }
}