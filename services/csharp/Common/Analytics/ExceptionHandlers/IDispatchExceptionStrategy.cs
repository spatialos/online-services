using System.Net.Http;

namespace Improbable.OnlineServices.Common.Analytics.ExceptionHandlers
{
    public interface IDispatchExceptionStrategy
    {
        void ProcessException(HttpRequestException e);
    }
}
