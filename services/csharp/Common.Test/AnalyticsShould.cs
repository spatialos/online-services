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
        private const string SourceVal = "event_source_value";
        private const string ClassVal = "event_class_value";
        private const string TypeVal = "event_type_value";
        private const string KeyVal = "gcp_key_value";

        // The default event category we expect when one isn't provided by a config file
        private const string DefaultEventCategory = "cold";

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
            Assert.IsInstanceOf<NullAnalyticsSender>(
                new AnalyticsSenderBuilder(AnalyticsEnvironment.Development, KeyVal, SourceVal).Build()
            );
        }

        [Test]
        public void BuildRealAnalyticsSenderIfProvidedWithEndpoint()
        {
            Assert.IsInstanceOf<AnalyticsSender>(
                new AnalyticsSenderBuilder(AnalyticsEnvironment.Development, KeyVal, SourceVal)
                    .WithCommandLineArgs( $"--{AnalyticsCommandLineArgs.EndpointName}", "https://example.com/" )
                    .Build()
            );
        }

        [Test]
        public void FailToBuildIfHttpIsNotUsedWithoutInsecureEnabled()
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(
                () =>
                    new AnalyticsSenderBuilder(AnalyticsEnvironment.Development, KeyVal, SourceVal)
                        .WithCommandLineArgs( $"--{AnalyticsCommandLineArgs.EndpointName}", "http://example.com/" )
                        .Build()
            );

            Assert.That(ex.Message, Contains.Substring("uses http, but only https is allowed"));
        }

        [Test]
        public void AllowsHttpIfInsecureEndpointsEnabled()
        {
            Assert.IsInstanceOf<AnalyticsSender>(
                new AnalyticsSenderBuilder(AnalyticsEnvironment.Development, KeyVal, SourceVal)
                    .WithCommandLineArgs(
                        $"--{AnalyticsCommandLineArgs.EndpointName}", "http://example.com/",
                        $"--{AnalyticsCommandLineArgs.AllowInsecureEndpointName}"
                    )
                    .Build()
            );
        }

        private bool SendAnalyticEventsToHttpsEndpointExpectedMessage(HttpRequestMessage request)
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
            Assert.AreEqual(DefaultEventCategory, queryCollection["event_category"]);
            Assert.True(Guid.TryParse((string) queryCollection["session_id"], out Guid _));

            return request.Method == HttpMethod.Post;
        }

        [Test]
        public void SendAnalyticEventsToHttpsEndpoint()
        {
            HttpClient client = new HttpClient(_messageHandlerMock.Object);
            new AnalyticsSenderBuilder(AnalyticsEnvironment.Development, KeyVal, SourceVal)
                .WithCommandLineArgs( $"--{AnalyticsCommandLineArgs.EndpointName}", "https://example.com/" )
                .With(client)
                .Build()
                .Send(ClassVal, TypeVal, new Dictionary<string, string>
                {
                    { "dogs", "excellent" }
                }, true);

            _messageHandlerMock.Protected().Verify("SendAsync", Times.Exactly(1),
                ItExpr.Is<HttpRequestMessage>(req => SendAnalyticEventsToHttpsEndpointExpectedMessage(req)),
                ItExpr.IsAny<CancellationToken>());
        }

        [Test]
        public async Task DispatchAnalyticsEventsForSameUriTogether()
        {
            HttpClient client = new HttpClient(_messageHandlerMock.Object);
            IAnalyticsSender sender = new AnalyticsSenderBuilder(DevEnv, KeyVal, SourceVal)
                .WithMaxQueueSize(3)
                .WithCommandLineArgs($"--{AnalyticsCommandLineArgs.EndpointName}", "https://example.com/")
                .With(client)
                .Build();

            await sender.Send(ClassVal, TypeVal, new Dictionary<string, string>());
            await sender.Send("class-val-2", "type-val-2", new Dictionary<string, string>());
            await sender.Send("class-val-3", "type-val-3", new Dictionary<string, string>());

            _messageHandlerMock.Protected().Verify("SendAsync", Times.Exactly(1),
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>());
        }

        [Test]
        public async Task DispatchAnalyticsEventsAfterSomeTime()
        {
            HttpClient client = new HttpClient(_messageHandlerMock.Object);
            IAnalyticsSender sender = new AnalyticsSenderBuilder(DevEnv, KeyVal, SourceVal)
                .WithMaxQueueSize(3)
                .WithMaxQueueTimeMs(5)
                .WithCommandLineArgs($"--{AnalyticsCommandLineArgs.EndpointName}", "https://example.com/")
                .With(client)
                .Build();

            await sender.Send(ClassVal, TypeVal, new Dictionary<string, string>());
            await sender.Send("class-val-2", "type-val-2", new Dictionary<string, string>());
            await Task.Delay(10);

            _messageHandlerMock.Protected().Verify("SendAsync", Times.Exactly(1),
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>());
        }

        [Test]
        public async Task NotDispatchAnalyticsEventsWithoutTimeOrQueueSize()
        {
            HttpClient client = new HttpClient(_messageHandlerMock.Object);
            IAnalyticsSender sender = new AnalyticsSenderBuilder(DevEnv, KeyVal, SourceVal)
                .WithMaxQueueSize(3)
                .WithMaxQueueTimeMs(5000)
                .WithCommandLineArgs($"--{AnalyticsCommandLineArgs.EndpointName}", "https://example.com/")
                .With(client)
                .Build();

            await sender.Send(ClassVal, TypeVal, new Dictionary<string, string>());
            await sender.Send("class-val-2", "type-val-2", new Dictionary<string, string>());

            _messageHandlerMock.Protected().Verify("SendAsync", Times.Exactly(0),
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>());
        }

        [Test]
        public void FallBackToDefaultConfigurationGracefully()
        {
            AnalyticsConfig config = new AnalyticsConfig("");
            Assert.AreEqual(config.GetCategory("c", "t"), 
                DefaultEventCategory);
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
            Assert.AreEqual(DefaultEventCategory, config.GetCategory("d", "e"));
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
