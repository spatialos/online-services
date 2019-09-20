using System;
using System.Collections.Generic;
using System.Linq;
using Grpc.Core;
using Improbable.OnlineServices.Common.Analytics;
using Improbable.OnlineServices.DataModel;
using Improbable.OnlineServices.DataModel.Party;
using Improbable.OnlineServices.Proto.Party;
using MemoryStore;
using Moq;
using NUnit.Framework;
using PartyDataModel = Improbable.OnlineServices.DataModel.Party.Party;
using PartyProto = Improbable.OnlineServices.Proto.Party.Party;

namespace Party.Test
{
    [TestFixture]
    public class CreatePartyShould
    {
        private const string TestLeaderPlayerId = "Gridelwald2018";
        private const uint TestMinMembers = 2;
        private const uint TestMaxMembers = 5;
        private const string Pit = "PIT";
        private const string AnalyticsEventType = "player_created_party";

        private static readonly Dictionary<string, string> _testMetadata = new Dictionary<string, string>
            {{"location", "Paris"}};

        private Mock<ITransaction> _mockTransaction;
        private Mock<IMemoryStoreClient> _mockMemoryStoreClient;
        private Mock<IAnalyticsSender> _mockAnalyticsSender;
        private PartyServiceImpl _partyService;

        [SetUp]
        public void SetUp()
        {
            _mockTransaction = new Mock<ITransaction>(MockBehavior.Strict);
            _mockTransaction.Setup(tr => tr.Dispose());
            _mockMemoryStoreClient = new Mock<IMemoryStoreClient>(MockBehavior.Strict);
            _mockMemoryStoreClient.Setup(client => client.CreateTransaction()).Returns(_mockTransaction.Object);
            _mockMemoryStoreClient.Setup(client => client.Dispose()).Verifiable();
            _mockAnalyticsSender = new Mock<IAnalyticsSender>(MockBehavior.Strict);
            var memoryStoreClientManager = new Mock<IMemoryStoreClientManager<IMemoryStoreClient>>(MockBehavior.Strict);
            memoryStoreClientManager.Setup(manager => manager.GetClient()).Returns(_mockMemoryStoreClient.Object);
            _partyService = new PartyServiceImpl(memoryStoreClientManager.Object, _mockAnalyticsSender.Object);
        }

        [Test]
        public void ReturnInvalidArgumentIfEncounteringErrorsWhileConstructingParty()
        {
            // Setup the client such that it will claim that the leader is not a member of another party.
            _mockMemoryStoreClient.Setup(client => client.GetAsync<Member>(TestLeaderPlayerId)).ReturnsAsync((Member) null);

            // Send a request for creating a party where the minimum number of members is higher than the maximum number
            // of members.
            var context = Util.CreateFakeCallContext(TestLeaderPlayerId, Pit);
            var exception = Assert.Throws<RpcException>(() =>
                _partyService.CreateParty(new CreatePartyRequest { MinMembers = 10, MaxMembers = 5 }, context));
            Assert.That(exception.Message, Contains.Substring("minimum number of members cannot be higher"));
            Assert.AreEqual(StatusCode.InvalidArgument, exception.StatusCode);
        }

        [Test]
        public void ReturnPartyIdWhenSuccessful()
        {
            // Setup the client such that it will claim that TestLeader isn't a member of any party and such it will
            // return a party id for some particular parameters.
            IEnumerable<Entry> created = null;
            _mockMemoryStoreClient.Setup(client => client.GetAsync<Member>(TestLeaderPlayerId))
                .ReturnsAsync((Member) null);
            _mockTransaction.Setup(tr => tr.CreateAll(It.IsAny<IEnumerable<Entry>>()))
                .Callback<IEnumerable<Entry>>(entries => created = entries);
            _mockAnalyticsSender.Setup(
                sender => sender.Send(AnalyticsConstants.PartyClass, AnalyticsEventType,
                                      It.Is<Dictionary<string, string>>(d => ExpectedAnalyticsDict(d)), TestLeaderPlayerId));

            // Verify that a party has been successfully created and that its id has been returned.
            var createPartyRequest = new CreatePartyRequest
            {
                MinMembers = TestMinMembers,
                MaxMembers = TestMaxMembers
            };
            createPartyRequest.Metadata.Add(_testMetadata);

            var context = Util.CreateFakeCallContext(TestLeaderPlayerId, Pit);
            var createPartyResponse = _partyService.CreateParty(createPartyRequest, context).Result;

            var entriesList = created.ToList();
            Assert.AreEqual(2, entriesList.Count);

            var party = (PartyDataModel) entriesList[0];
            Assert.False(string.IsNullOrEmpty(party.Id));
            Assert.AreEqual(TestLeaderPlayerId, party.LeaderPlayerId);
            Assert.AreEqual(TestMinMembers, party.MinMembers);
            Assert.AreEqual(TestMaxMembers, party.MaxMembers);
            CollectionAssert.AreEquivalent(_testMetadata, party.Metadata);
            Assert.That(party.MemberIdToPit, Contains.Key(TestLeaderPlayerId));
            Assert.AreEqual(party.MemberIdToPit[TestLeaderPlayerId], Pit);

            var leader = (Member) entriesList[1];
            Assert.AreEqual(TestLeaderPlayerId, leader.Id);
            Assert.AreEqual(party.Id, leader.PartyId);
            Assert.AreEqual(party.Id, createPartyResponse.PartyId);

            _mockAnalyticsSender.VerifyAll();
        }

        bool ExpectedAnalyticsDict(Dictionary<string, string> analyticsDict)
        {
            return analyticsDict[AnalyticsConstants.PlayerId] == TestLeaderPlayerId
                && Guid.TryParse(analyticsDict[AnalyticsConstants.PartyId], out _)
                && analyticsDict.Count == 2;
        }
    }
}
