using System.Linq;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Core.Interceptors;

namespace Improbable.MetagameServices.Common.Interceptors
{
    public class SecretCheckingInterceptor : Interceptor
    {
        private const string SecretHeaderKey = "x-auth-secret";

        private readonly string _secret;

        public SecretCheckingInterceptor(string secret)
        {
            _secret = secret;
        }

        public override Task<TResponse> UnaryServerHandler<TRequest, TResponse>(TRequest request,
            ServerCallContext context,
            UnaryServerMethod<TRequest, TResponse> continuation)
        {
            var validated = context.RequestHeaders.SingleOrDefault(item => item.Key == SecretHeaderKey)?.Value ==
                            _secret;
            AuthHeaders.AddAuthenticatedHeaderToContext(context, validated);
            return continuation(request, context);
        }
    }
}
