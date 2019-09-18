using System.Collections.Generic;
using Newtonsoft.Json;

namespace Improbable.OnlineServices.DataModel
{
    public abstract class Entry
    {
        public string Id { get; protected set; }
        
        [JsonIgnore] public string PreviousState { get; set; }

        public string SerializeToJson()
        {
            return JsonConvert.SerializeObject(this);
        }

        public IEnumerable<Entry> Yield()
        {
            yield return this;
        }
    }
}
