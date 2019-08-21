using System;
using System.Threading;
using Grpc.Core;
using Grpc.Core.Testing;

namespace GatewayInternal.Test
{
    public class Util
    {
        public static ServerCallContext CreateFakeCallContext()
        {
            return TestServerCallContext.Create(
                "", "", DateTime.Now + TimeSpan.FromHours(1), Metadata.Empty,
                CancellationToken.None, "", null, null, meta => null, () => WriteOptions.Default, writeOptions => { }
            );
        }
    }
}
