using System.Collections.Generic;
using System.Threading.Tasks;

namespace Improbable.OnlineServices.Common.Analytics
{
    public interface IAnalyticsSender
    {
        Task Send(string eventClass, string eventType, Dictionary<string, string> eventAttributes);
    }
}