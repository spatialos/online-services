using System;
using System.IO;
using System.Threading.Tasks;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using EntriesList = System.Collections.Generic.Dictionary<string,
    System.Collections.Generic.Dictionary<string,
        Improbable.OnlineServices.Common.Analytics.Config.AnalyticsConfig.AnalyticsConfigEntry>>;

namespace Improbable.OnlineServices.Common.Analytics.Config
{
    /// <summary>
    /// Given an analytics configuration string, allows querying to determine e.g. event category category and whether
    ///   or not given event types are enabled.
    /// </summary>
    public class AnalyticsConfig
    {
        internal class AnalyticsConfigEntry
        {
            internal static readonly AnalyticsConfigEntry DefaultEntry = new AnalyticsConfigEntry();

            public string Category { get; set; } = AnalyticsSender.DefaultEventCategory;
            public bool Disabled { get; set; } = false;
        }

        public static AnalyticsConfig FromFile(string filePath)
        {
            return new AnalyticsConfig(File.ReadAllText(filePath));
        }

        // Type defined at head of file
        private const string Wildcard = "*";
        private readonly EntriesList _entries;

        public AnalyticsConfig()
        {
            _entries = new EntriesList();
        }

        public AnalyticsConfig(string contents)
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(new CamelCaseNamingConvention())
                .Build();
            try
            {
                _entries = deserializer.Deserialize<EntriesList>(contents) ?? new EntriesList();
            }
            catch (YamlException e)
            {
                throw new ArgumentException("Invalid YAML was provided", e);
            }
        }

        public bool IsEnabled(string eventClass, string eventType) => !GetEntry(eventClass, eventType).Disabled;
        public string GetCategory(string eventClass, string eventType) => GetEntry(eventClass, eventType).Category;

        /// <summary>
        /// Gets the most specific entry possible for a given event class/type pair.
        /// The priority is to match an exact event class, then an exact event type, so in the case where it can choose
        ///   a wildcard class and a specific type _or_ a specific class and a wildcard type, it will opt for the latter.
        /// </summary>
        private AnalyticsConfigEntry GetEntry(string eventClass, string eventType)
        {
            if (_entries.ContainsKey(eventClass))
            {
                if (_entries[eventClass].ContainsKey(eventType))
                {
                    return _entries[eventClass][eventType];
                }
                else if (_entries[eventClass].ContainsKey(Wildcard))
                {
                    return _entries[eventClass][Wildcard];
                }
            }

            if (_entries.ContainsKey(Wildcard))
            {
                if (_entries[Wildcard].ContainsKey(eventType))
                {
                    return _entries[Wildcard][eventType];
                }
                else if (_entries[Wildcard].ContainsKey(Wildcard))
                {
                    return _entries[Wildcard][Wildcard];
                }
            }

            return AnalyticsConfigEntry.DefaultEntry;
        }
    }
}
