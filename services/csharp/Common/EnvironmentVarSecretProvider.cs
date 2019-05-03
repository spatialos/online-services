using System;
using System.Collections.Generic;
using System.Linq;

namespace Improbable.OnlineServices.Common
{
    public class EnvironmentVarSecretProvider : ISecretProvider
    {
        private readonly Dictionary<string, string> _defaultEntries;

        public EnvironmentVarSecretProvider(): this(null)
        {
        }

        public EnvironmentVarSecretProvider(IDictionary<string, string> keyValuePairs)
        {
            // create a copy of the dictionary
            _defaultEntries = keyValuePairs?.ToDictionary(
                entry => entry.Key,
                entry => entry.Value);
        }

        public string this[string key]
        {
            get
            {
                var result = Environment.GetEnvironmentVariable(key) ?? _defaultEntries?[key];
                if (result == null)
                    throw new KeyNotFoundException();

                return result;
            }
        }
    }
}