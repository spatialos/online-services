using System;
using System.Threading;
using Grpc.Core;
using Grpc.Core.Testing;

namespace Gateway.Test
{
    public static class Util
    {
        private const string PlayerIdentifierHeader = "x-internal-player-identifier";
        private const string PlayerIdentityTokenHeader = "player-identity-token";

        public static ServerCallContext CreateFakeCallContext(string playerId, string pit)
        {
            var metadata = new Metadata { { PlayerIdentifierHeader, playerId }, { PlayerIdentityTokenHeader, pit } };
            var context = TestServerCallContext.Create(
                "", "", DateTime.Now + TimeSpan.FromHours(1), metadata, CancellationToken.None, "", null, null,
                meta => null, () => WriteOptions.Default, writeOptions => { });
            return context;
        }
    }
}
