using System;
using System.Threading;
using Grpc.Core;
using Grpc.Core.Testing;

namespace DeploymentMetadata.Test
{
    public static class Util
    {
        private const string SecretHeader = "x-secretkey";

        public static ServerCallContext CreateFakeCallContext(string secret)
        {
            var metadata = new Metadata { { SecretHeader, secret } };
            var context = TestServerCallContext.Create(
                "", "", DateTime.Now + TimeSpan.FromHours(1), metadata, CancellationToken.None, "", null, null,
                meta => null, () => WriteOptions.Default, writeOptions => { });
            return context;
        }
    }
}
