using System.Collections.Generic;
using System.Threading.Tasks;

namespace Improbable.OnlineServices.Common.Analytics
{
    public class AnalyticsSenderClassWrapper : IAnalyticsSender
    {
        private readonly IAnalyticsSender _wrapped;
        private readonly string _eventClass;

        public AnalyticsSenderClassWrapper(IAnalyticsSender wrapped, string eventClass)
        {
            _wrapped = wrapped;
            _eventClass = eventClass;
        }

        public void Send<T>(string eventClass, string eventType, Dictionary<string, T> eventAttributes)
        {
            _wrapped.Send(eventClass, eventType, eventAttributes);
        }

        public Task SendAsync<T>(string eventClass, string eventType, Dictionary<string, T> eventAttributes)
        {
            return _wrapped.SendAsync(eventClass, eventType, eventAttributes);
        }

        public void Send<T>(string eventType, Dictionary<string, T> eventAttributes)
        {
            Send(_eventClass, eventType, eventAttributes);
        }

        public Task SendAsync<T>(string eventType, Dictionary<string, T> eventAttributes)
        {
            return SendAsync(_eventClass, eventType, eventAttributes);
        }

        /// <summary>
        /// The owner of the analytics sender is still responsible for disposing it.
        /// </summary>
        public void Dispose()
        {
        }
    }
}
