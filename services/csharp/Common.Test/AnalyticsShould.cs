using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Improbable.OnlineServices.Common.Analytics;
using Moq;
using Moq.Protected;
using Newtonsoft.Json;
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
                new[] { $"--{EndpointName}", "https://example.com/" },
                AnalyticsEnvironment.Development, ""));
        }

        [Test]
        public void FailToBuildIfHttpIsNotUsedWithoutInsecureEnabled()
        {
            Assert.Throws(typeof(ArgumentException),
                () => AnalyticsSender.Build(new[] { $"--{EndpointName}", "http://example.com/" },
                    AnalyticsEnvironment.Development, ""));
        }

        [Test]
        public void AllowsHttpIfInsecureEndpointsEnabled()
        {
            Assert.IsInstanceOf<AnalyticsSender>(AnalyticsSender.Build(
                new[] { $"--{EndpointName}", "http://example.com/", $"--{AllowInsecureEndpointName}" },
                AnalyticsEnvironment.Development, ""));
        }

        private bool ExpectedMessage(HttpRequestMessage request)
        {
            Assert.IsInstanceOf<StringContent>(request.Content);
            if (request.Content is StringContent messageContent)
            {
                dynamic content = JsonConvert.DeserializeObject(messageContent.ReadAsStringAsync().Result);

                // TODO: Test versioning when it is added
                Assert.AreEqual(content.eventEnvironment.Value, "development");
                Assert.AreEqual(content.eventIndex.Value, "0");
                Assert.AreEqual(content.eventSource.Value, "source");
                Assert.AreEqual(content.eventClass.Value, "test");
                Assert.AreEqual(content.eventType.Value, "send");
                Assert.True(Guid.TryParse(content.sessionId.Value, out Guid _));

                // Check the timestamp is within 5 seconds of now (i.e. roughly correct)
                long unixTimestampDelta = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                                          - long.Parse(content.eventTimestamp.Value);
                Assert.GreaterOrEqual(unixTimestampDelta, 0);
                Assert.Less(unixTimestampDelta, 5);

                dynamic eventContent = JsonConvert.DeserializeObject(content.eventAttributes.Value);
                Assert.AreEqual(eventContent.dogs.Value, "excellent");
            }

            var queryCollection = request.RequestUri.ParseQueryString();
            Assert.AreEqual(queryCollection["key"], "fakeKey");
            Assert.AreEqual(queryCollection["analytics_environment"], "development");
            // TODO: Update with real category
            Assert.AreEqual(queryCollection["event_category"], "");
            Assert.True(Guid.TryParse(queryCollection["session_id"], out Guid _));

            return request.Method == HttpMethod.Post;
        }

        [Test]
        public void SendAnalyticEventsToHttpsEndpoint()
        {
            HttpClient client = new HttpClient(_messageHandlerMock.Object);
            AnalyticsSender.Build(new[] { $"--{EndpointName}", "https://example.com/" },
                    AnalyticsEnvironment.Development, "fakeKey", "source", client)
                .Send("test", "send", new Dictionary<string, string>
                {
                    {"dogs", "excellent"}
                });

            // TODO: Verify contents of the message
            _messageHandlerMock.Protected().Verify("SendAsync", Times.Exactly(1),
                ItExpr.Is<HttpRequestMessage>(req => ExpectedMessage(req)),
                ItExpr.IsAny<CancellationToken>());

        }
    }
}