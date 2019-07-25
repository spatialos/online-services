using System.Collections.Generic;

namespace Improbable.OnlineServices.DataModel.Party
{
    public static class MetadataUpdater
    {
        public static void Update(IDictionary<string, string> metadata, IDictionary<string, string> updates)
        {
            foreach (var (key, value) in updates)
            {
                if (string.IsNullOrEmpty(value))
                {
                    metadata.Remove(key);
                    continue;
                }

                metadata[key] = value;
            }
        }
    }
}
