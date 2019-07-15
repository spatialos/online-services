using System;

namespace Improbable.MetagameServices.Common
{
    public class Secrets
    {
        public static string GetEnvSecret(string secretName)
        {
            var envVar = Environment.GetEnvironmentVariable(secretName) ?? throw new Exception($"EnvVar {secretName} is required.");
            if (string.IsNullOrEmpty(envVar))
            {
                throw new ArgumentException($"EnvVar {secretName} should not be empty.");
            }

            return envVar;
        }
    }
}
