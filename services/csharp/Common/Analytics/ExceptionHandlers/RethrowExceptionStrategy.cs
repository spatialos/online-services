using System.Net.Http;

namespace Improbable.OnlineServices.Common.Analytics.ExceptionHandlers
{
    public class RethrowExceptionStrategy : IDispatchExceptionStrategy
    {
        public void ProcessException(HttpRequestException e)
        {
            // Rethrow the exception
            throw e;
        }
    }
}
