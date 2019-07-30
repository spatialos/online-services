using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Improbable.OnlineServices.Common.Analytics;
using Improbable.OnlineServices.Common.Analytics.Config;
using Moq;
using Moq.Protected;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Improbable.OnlineServices.Common.Test
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

        private AnalyticsConfig _emptyConfig = new AnalyticsConfig("");

        [Test]
        public void BuildNullByDefault()
        {
            Assert.IsInstanceOf<NullAnalyticsSender>(AnalyticsSender.Build(
                    new string[] { }, AnalyticsEnvironment.Development, _emptyConfig, ""
                )
            );
        }

        [Test]
        public void BuildRealAnalyticsSenderIfProvidedWithEndpoint()
        {
            Assert.IsInstanceOf<AnalyticsSender>(AnalyticsSender.Build(
                    new[] {$"--{AnalyticsCommandLineArgs.EndpointName}", "https://example.com/"},
                    AnalyticsEnvironment.Development, _emptyConfig, ""
                )
            );
        }

        [Test]
        public void FailToBuildIfHttpIsNotUsedWithoutInsecureEnabled()
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(
                () => AnalyticsSender.Build(
                    new[] {$"--{AnalyticsCommandLineArgs.EndpointName}", "http://example.com/"},
                    AnalyticsEnvironment.Development, _emptyConfig, ""
                )
            );

            Assert.That(ex.Message, Contains.Substring("uses http, but only https is allowed"));
        }

        [Test]
        public void AllowsHttpIfInsecureEndpointsEnabled()
        {
            Assert.IsInstanceOf<AnalyticsSender>(
                AnalyticsSender.Build(
                    new[]
                    {
                        $"--{AnalyticsCommandLineArgs.EndpointName}", "http://example.com/",
                        $"--{AnalyticsCommandLineArgs.AllowInsecureEndpointName}"
                    },
                    AnalyticsEnvironment.Development, _emptyConfig, ""
                )
            );
        }

        private const string SourceVal = "event_source_value";
        private const string ClassVal = "event_class_value";
        private const string TypeVal = "event_type_value";
        private const string KeyVal = "fakeKey";

        private bool ExpectedMessage(HttpRequestMessage request)
        {
            var development = AnalyticsEnvironment.Development.ToString().ToLower();

            Assert.IsInstanceOf<StringContent>(request.Content);
            if (request.Content is StringContent messageContent)
            {
                dynamic content = JsonConvert.DeserializeObject(messageContent.ReadAsStringAsync().Result);

                // TODO: Test versioning when it is added
                Assert.AreEqual(development, content.eventEnvironment.Value);
                Assert.AreEqual("0", content.eventIndex.Value);
                Assert.AreEqual(SourceVal, content.eventSource.Value);
                Assert.AreEqual(ClassVal, content.eventClass.Value);
                Assert.AreEqual(TypeVal, content.eventType.Value);
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
            Assert.AreEqual(KeyVal, queryCollection["key"]);
            Assert.AreEqual(development, queryCollection["analytics_environment"]);
            // TODO: Update with real category
            Assert.AreEqual(AnalyticsSender.DefaultEventCategory, queryCollection["event_category"]);
            Assert.True(Guid.TryParse(queryCollection["session_id"], out Guid _));

            return request.Method == HttpMethod.Post;
        }

        [Test]
        public void SendAnalyticEventsToHttpsEndpoint()
        {
            HttpClient client = new HttpClient(_messageHandlerMock.Object);
            AnalyticsSender.Build(new[] {$"--{AnalyticsCommandLineArgs.EndpointName}", "https://example.com/"},
                    AnalyticsEnvironment.Development, _emptyConfig, KeyVal, SourceVal, client)
                .Send(ClassVal, TypeVal, new Dictionary<string, string>
                {
                    {"dogs", "excellent"}
                });

            // TODO: Verify contents of the message
            _messageHandlerMock.Protected().Verify("SendAsync", Times.Exactly(1),
                ItExpr.Is<HttpRequestMessage>(req => ExpectedMessage(req)),
                ItExpr.IsAny<CancellationToken>());
        }

        [Test]
        public void FallBackToDefaultConfigurationGracefully()
        {
            AnalyticsConfig config = new AnalyticsConfig("");
            Assert.AreEqual(config.GetCategory("c", "t"), AnalyticsSender.DefaultEventCategory);
            Assert.True(config.IsEnabled("c", "t"));
        }

        private readonly string _configString = @"
'*':
  '*':
    disabled: true
  'a':
    category: 'function'
'b':
  '*':
    category: 'function-2'
  'c':
    category: 'function-3'
";

        [Test]
        public void HandleConfigPrecedenceRulesCorrectly()
        {
            AnalyticsConfig config = new AnalyticsConfig(_configString);

            // d.e should route to *.* as there is no match
            Assert.False(config.IsEnabled("d", "e"));
            Assert.AreEqual(AnalyticsSender.DefaultEventCategory, config.GetCategory("d", "e"));

            // d.a should route to *.a as there is not a better match
            Assert.True(config.IsEnabled("d", "a"));
            Assert.AreEqual("function", config.GetCategory("d", "a"));

            // b.d should route to b.* as there is not a better match
            Assert.True(config.IsEnabled("b", "d"));
            Assert.AreEqual("function-2", config.GetCategory("b", "d"));

            // b.a should route to b.* as a match on class is preferred on a match on type (i.e. b.* > *.a)
            Assert.True(config.IsEnabled("b", "a"));
            Assert.AreEqual("function-2", config.GetCategory("b", "a"));

            // b.c should route to b.c as it is an exact match
            Assert.True(config.IsEnabled("b", "c"));
            Assert.AreEqual("function-3", config.GetCategory("b", "c"));
        }
    }
}
