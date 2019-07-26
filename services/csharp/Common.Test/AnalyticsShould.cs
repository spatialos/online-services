using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Improbable.OnlineServices.Common.Analytics;
using Moq;
using Moq.Protected;
using NUnit.Framework;
using static Improbable.OnlineServices.Common.Analytics.AnalyticsCommandLineArgs;

namespace Common.Test
{
    public class AnalyticsShould
    {
        private Mock<HttpMessageHandler> _messageHandlerMock;

        [SetUp]
        public void Setup()
        {
            _messageHandlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            _messageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                ).ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("")
                }).Verifiable();
        }

        [Test]
        public void BuildNullByDefault()
        {
            Assert.IsInstanceOf<NullAnalyticsSender>(AnalyticsSender.Build(new string[] { },
                AnalyticsEnvironment.Development, ""));
        }

        [Test]
        public void BuildRealAnalyticsSenderIfProvidedWithEndpoint()
        {
            Assert.IsInstanceOf<AnalyticsSender>(AnalyticsSender.Build(
                new[] {$"--{EndpointName}", "https://example.com/"},
                AnalyticsEnvironment.Development, ""));
        }

        [Test]
        public void FailToBuildIfHttpIsNotUsedWithoutInsecureEnabled()
        {
            Assert.Throws(typeof(ArgumentException),
                () => AnalyticsSender.Build(new[] {$"--{EndpointName}", "http://example.com/"},
                    AnalyticsEnvironment.Development, ""));
        }

        [Test]
        public void AllowsHttpIfInsecureEndpointsEnabled()
        {
            Assert.IsInstanceOf<AnalyticsSender>(AnalyticsSender.Build(
                new[] {$"--{EndpointName}", "http://example.com/", $"--{AllowInsecureEndpointName}"},
                AnalyticsEnvironment.Development, ""));
        }

        [Test]
        public void SendAnalyticEventsToHttpsEndpoint()
        {
            HttpClient client = new HttpClient(_messageHandlerMock.Object);
            AnalyticsSender.Build(new[] {$"--{EndpointName}", "https://example.com/"},
                    AnalyticsEnvironment.Development, "", "", client)
                .Send("test", "send", new Dictionary<string, string>
                {
                    {"dogs", "excellent"}
                });

            // TODO: Verify contents of the message
            _messageHandlerMock.Protected().Verify("SendAsync", Times.Exactly(1),
                ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Post),
                ItExpr.IsAny<CancellationToken>());
        }
    }
}