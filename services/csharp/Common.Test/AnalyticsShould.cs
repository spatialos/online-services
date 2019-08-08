using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Improbable.OnlineServices.Common.Analytics;
using Improbable.OnlineServices.Common.Analytics.Config;
using Improbable.OnlineServices.Common.Analytics.ExceptionHandlers;
using Moq;
using Moq.Protected;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Serilog;

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
            _messageHandlerMock = new Mock<HttpMessageHandler>(MockBehavior.Loose);
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
                new AnalyticsSenderBuilder(AnalyticsEnvironment.Testing, KeyVal, SourceVal).Build()
            );
        }

        [Test]
        public void BuildRealAnalyticsSenderIfProvidedWithEndpoint()
        {
            using (var analyticsSender =
                new AnalyticsSenderBuilder(AnalyticsEnvironment.Testing, KeyVal, SourceVal)
                    .WithCommandLineArgs($"--{AnalyticsCommandLineArgs.EndpointName}", "https://example.com/")
                    .Build())
            {
                Assert.IsInstanceOf<AnalyticsSender>(analyticsSender);
            }
        }

        [Test]
        public void FailToBuildIfHttpIsNotUsedWithoutInsecureEnabled()
        {
            var ex = Assert.Throws<ArgumentException>(
                () =>
                    new AnalyticsSenderBuilder(AnalyticsEnvironment.Testing, KeyVal, SourceVal)
                        .WithCommandLineArgs($"--{AnalyticsCommandLineArgs.EndpointName}", "http://example.com/")
                        .Build()
            );

            Assert.That(ex.Message, Contains.Substring("uses http, but only https is allowed"));
        }

        [Test]
        public void AllowsHttpIfInsecureEndpointsEnabled()
        {
            using (var sender =
                new AnalyticsSenderBuilder(AnalyticsEnvironment.Testing, KeyVal, SourceVal)
                    .WithCommandLineArgs(
                        $"--{AnalyticsCommandLineArgs.EndpointName}", "http://example.com/",
                        $"--{AnalyticsCommandLineArgs.AllowInsecureEndpointName}"
                    )
                    .Build())
            {
                Assert.IsInstanceOf<AnalyticsSender>(sender);
            }
        }

        private bool SendAnalyticEventsToHttpsEndpointExpectedMessage(HttpRequestMessage request)
        {
            var environment = AnalyticsEnvironment.Testing.ToString().ToLower();

            Assert.IsInstanceOf<StringContent>(request.Content);
            if (request.Content is StringContent messageContent)
            {
                dynamic content = JsonConvert.DeserializeObject(messageContent.ReadAsStringAsync().Result);

                // TODO: Test versioning when it is added
                Assert.AreEqual(environment, content.eventEnvironment.Value);
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

                var eventContent = JsonConvert.DeserializeObject(content.eventAttributes.Value);
                Assert.AreEqual(eventContent.dogs.Value, "excellent");

                var mammals = ((JArray) eventContent.animals.mammals).Values<string>().ToList();
                var lizards = ((JArray) eventContent.animals.lizards).Values<string>().ToList();
                Assert.Contains("dolphins", mammals);
                Assert.Contains("cats", mammals);
                Assert.Contains("iguanas", lizards);
                Assert.Contains("chameleons", lizards);
            }

            var queryCollection = request.RequestUri.ParseQueryString();
            Assert.AreEqual(KeyVal, queryCollection["key"]);
            Assert.AreEqual(environment, queryCollection["analytics_environment"]);
            Assert.AreEqual(DefaultEventCategory, queryCollection["event_category"]);
            Assert.True(Guid.TryParse(queryCollection["session_id"], out var _));

            return request.Method == HttpMethod.Post;
        }

        [Test]
        public async Task SendAnalyticEventsToHttpsEndpoint()
        {
            var client = new HttpClient(_messageHandlerMock.Object);
            using (var sender =
                new AnalyticsSenderBuilder(AnalyticsEnvironment.Testing, KeyVal, SourceVal)
                    .WithCommandLineArgs($"--{AnalyticsCommandLineArgs.EndpointName}", "https://example.com/")
                    .WithMaxQueueTime(TimeSpan.FromMilliseconds(5))
                    .With(client)
                    .Build())
            {
                await sender.SendAsync(ClassVal, TypeVal, new Dictionary<string, object>
                {
                    { "dogs", "excellent" },
                    { "animals", new Dictionary<string, List<string>>
                    {
                        { "mammals", new List<string> { "dolphins", "cats" }},
                        { "lizards", new List<string> { "iguanas", "chameleons" }}
                    }}
                });

                await Task.Delay(TimeSpan.FromMilliseconds(20));

                _messageHandlerMock.Protected().Verify("SendAsync", Times.Exactly(1),
                    ItExpr.Is<HttpRequestMessage>(req => SendAnalyticEventsToHttpsEndpointExpectedMessage(req)),
                    ItExpr.IsAny<CancellationToken>());
            }
        }

        [Test]
        public async Task DispatchAnalyticsEventsAfterQueueFills()
        {
            var client = new HttpClient(_messageHandlerMock.Object);
            using (var sender =
                new AnalyticsSenderBuilder(AnalyticsEnvironment.Testing, KeyVal, SourceVal)
                    .WithMaxQueueSize(3)
                    // This test will hopefully not take a year to run
                    .WithMaxQueueTime(TimeSpan.FromDays(365.25))
                    .WithCommandLineArgs($"--{AnalyticsCommandLineArgs.EndpointName}", "https://example.com/")
                    .With(client)
                    .Build())
            {
                await sender.SendAsync(ClassVal, TypeVal, new Dictionary<string, string>());
                await sender.SendAsync("class-val-2", "type-val-2", new Dictionary<string, string>());
                await sender.SendAsync("class-val-3", "type-val-3", new Dictionary<string, string>());

                _messageHandlerMock.Protected().Verify("SendAsync", Times.Exactly(1),
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>());
            }
        }

        [Test]
        public async Task DispatchAnalyticsEventsAfterSomeTime()
        {
            var client = new HttpClient(_messageHandlerMock.Object);
            using (var sender =
                new AnalyticsSenderBuilder(AnalyticsEnvironment.Testing, KeyVal, SourceVal)
                    .WithMaxQueueTime(TimeSpan.FromMilliseconds(5))
                    .WithCommandLineArgs($"--{AnalyticsCommandLineArgs.EndpointName}", "https://example.com/")
                    .With(client)
                    .Build())
            {
                await sender.SendAsync(ClassVal, TypeVal, new Dictionary<string, string>());
                await sender.SendAsync("class-val-2", "type-val-2", new Dictionary<string, string>());
                await Task.Delay(20);

                _messageHandlerMock.Protected().Verify("SendAsync", Times.Exactly(1),
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>());
            }
        }

        [Test]
        public async Task NotDispatchAnalyticsEventsWithoutTime()
        {
            var client = new HttpClient(_messageHandlerMock.Object);
            using (var sender =
                new AnalyticsSenderBuilder(AnalyticsEnvironment.Testing, KeyVal, SourceVal)
                    .WithMaxQueueTime(TimeSpan.FromSeconds(5))
                    .WithCommandLineArgs($"--{AnalyticsCommandLineArgs.EndpointName}", "https://example.com/")
                    .With(client)
                    .Build())
            {
                await sender.SendAsync(ClassVal, TypeVal, new Dictionary<string, string>());
                await sender.SendAsync("class-val-2", "type-val-2", new Dictionary<string, string>());

                _messageHandlerMock.Protected().Verify("SendAsync", Times.Exactly(0),
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>());
            }
        }

        [Test]
        public void FallBackToDefaultConfigurationGracefully()
        {
            var config = new AnalyticsConfig("");
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
            var config = new AnalyticsConfig(_configString);

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

        [Test]
        public void LogWithLoggingExceptionStrategy()
        {
            Mock<ILogger> logMock = new Mock<ILogger>();
            HttpRequestException e = new HttpRequestException();

            new LogExceptionStrategy(logMock.Object).ProcessException(e);
            logMock.Verify(l => l.Error(e, It.IsAny<string>()), Times.Once());
        }

        [Test]
        public void ThrowWithRethrowExceptionStrategy()
        {
            Assert.Throws<HttpRequestException>(
                () => new RethrowExceptionStrategy().ProcessException(new HttpRequestException())
            );
        }

        [Test]
        public async Task CallIntoExceptionStrategyWhenHttpRequestFails()
        {
            Mock<IDispatchExceptionStrategy> strategyMock = new Mock<IDispatchExceptionStrategy>();
            HttpRequestException exception = new HttpRequestException();

            Mock<HttpMessageHandler> httpReqHandlerMock = new Mock<HttpMessageHandler>();
            httpReqHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .Throws(exception)
                .Verifiable();

            using (var sender =
                new AnalyticsSenderBuilder(AnalyticsEnvironment.Testing, KeyVal, SourceVal)
                    .WithMaxQueueSize(1)
                    .WithCommandLineArgs($"--{AnalyticsCommandLineArgs.EndpointName}", "https://example.com/")
                    .With(strategyMock.Object)
                    .With(new HttpClient(httpReqHandlerMock.Object))
                    .Build())
            {
                await sender.SendAsync(ClassVal, TypeVal, new Dictionary<string, string>());

                strategyMock.Verify(s => s.ProcessException(exception), Times.Once());
            }
        }
    }
}
