using System.Collections.Generic;
using Newtonsoft.Json;

namespace Improbable.OnlineServices.DataModel
{
    public abstract class QueuedEntry : Entry
    {
        [JsonIgnore] public double Score { get; set; }

        [JsonIgnore] public string QueueName { get; set; }

        public new IEnumerable<QueuedEntry> Yield()
        {
            yield return this;
        }
    }
}