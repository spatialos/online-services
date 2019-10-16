using System;
using System.Threading;
using Grpc.Core;
using Grpc.Core.Testing;
using Improbable.OnlineServices.Common;

namespace DeploymentMetadata.Test
{
    public static class Util
    {
        public static ServerCallContext CreateFakeCallContext(Auth authentication)
        {
            var context = TestServerCallContext.Create(
                "", "", DateTime.Now + TimeSpan.FromHours(1), new Metadata(), CancellationToken.None, "", null, null,
                meta => null, () => WriteOptions.Default, writeOptions => { });
            AuthHeaders.AddAuthenticatedHeaderToContext(context, authentication == Auth.Authenticated);
            return context;
        }

        public enum Auth
        {
            NotAutenticated,
            Authenticated
        }
    }
}
