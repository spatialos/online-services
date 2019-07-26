using System.Collections.Generic;

namespace Improbable.OnlineServices.Common.Analytics
{
    public interface IAnalyticsSender
    {
        void Send(string eventClass, string eventType, Dictionary<string, string> eventAttributes);
    }
}