using Improbable.MetagameServices.DataModel;

namespace MemoryStore.Redis
{
    public static class Key
    {
        // The format is <entry_type>:<entry_id>, where type is equivalent to the class name of the entry.
        private const string EntryKeyFormat = "{0}:{1}";

        // Use two colons to avoid collisions with entry keys.
        private const string QueueKeyFormat = "Queue::{0}";

        public static string ForEntry(Entry entry)
        {
            return string.Format(EntryKeyFormat, entry.GetType().Name, entry.Id);
        }

        public static string For<T>(string id) where T : Entry
        {
            return string.Format(EntryKeyFormat, typeof(T).Name, id);
        }

        public static string ForQueue(string queueName)
        {
            return string.Format(QueueKeyFormat, queueName);
        }
    }
}
