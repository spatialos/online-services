using System.Collections.Generic;
using System.Threading.Tasks;

namespace Improbable.OnlineServices.Common.Analytics
{
    public class AnalyticsSenderClassWrapper : IAnalyticsSender
    {
        private readonly IAnalyticsSender _wrapped;
        private readonly string _class;

        public AnalyticsSenderClassWrapper(IAnalyticsSender wrapped, string @class)
        {
            _wrapped = wrapped;
            _class = @class;
        }

        public void Send<T>(string eventClass, string eventType, Dictionary<string, T> eventAttributes)
        {
            _wrapped.Send<T>(eventClass, eventType, eventAttributes);
        }

        public Task SendAsync<T>(string eventClass, string eventType, Dictionary<string, T> eventAttributes)
        {
            return _wrapped.SendAsync<T>(eventClass, eventType, eventAttributes);
        }

        public void Send<T>(string eventType, Dictionary<string, T> eventAttributes)
        {
            Send(_class, eventType, eventAttributes);
        }

        public Task SendAsync<T>(string eventType, Dictionary<string, T> eventAttributes)
        {
            return SendAsync<T>(_class, eventType, eventAttributes);
        }

        public void Dispose()
        {
            _wrapped.Dispose();
        }
    }
}
