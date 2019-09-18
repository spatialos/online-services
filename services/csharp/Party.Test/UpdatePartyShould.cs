using System.Collections.Generic;
using Grpc.Core;
using Improbable.OnlineServices.Common.Analytics;
using Improbable.OnlineServices.DataModel;
using Improbable.OnlineServices.Proto.Party;
using MemoryStore;
using Moq;
using NUnit.Framework;
using PartyDataModel = Improbable.OnlineServices.DataModel.Party.Party;
using PartyProto = Improbable.OnlineServices.Proto.Party.Party;

namespace Party.Test
{
    [TestFixture]
    public class UpdatePartyShould
    {
        private const string TestPlayerId = "Gridelwald2018";
        private const string Pit = "PIT";
        private const uint TestMinMembers = 1;
        private const uint TestMaxMembers = 5;
        private const string TestPlayerId2 = "Credence2019";

        private const uint TestNewMinMembers = 100;
        private const uint TestNewMaxMembers = 1000;
        private const string AnalyticsEventType = "player_updated_party";
        private const string AnalyticsNewPartyState = "newPartyState";
        private const string AnalyticsPartyLeaderId = "partyLeaderId";
        private const string AnalyticsMaxMembers = "maxMembers";
        private const string AnalyticsMinMembers = "minMembers";

        private static readonly Dictionary<string, string> _testMetadata = new Dictionary<string, string>
            {{"platform", "Paris"}};

        private static readonly Dictionary<string, string> _testNewMetadata = new Dictionary<string, string>
        {
            {"enemy", "Dumbledore"},
            {"platform", ""},
        };

        private static readonly PartyProto _testUpdatedParty = new PartyProto
        {
            LeaderPlayerId = TestPlayerId2,
            MinMembers = TestNewMinMembers,
            MaxMembers = TestNewMaxMembers,
            CurrentPhase = PartyProto.Types.Phase.Matchmaking,
        };

        private static PartyDataModel _testParty;

        private Mock<ITransaction> _mockTransaction;
        private Mock<IMemoryStoreClient> _mockMemoryStoreClient;
        private Mock<IAnalyticsSender> _mockAnalyticsSender;
        private PartyServiceImpl _partyService;

        [SetUp]
        public void SetUp()
        {
            _testParty = new PartyDataModel(TestPlayerId, Pit, TestMinMembers, TestMaxMembers, _testMetadata);
            _testParty.MemberIdToPit[TestPlayerId2] = Pit;

            _testUpdatedParty.Id = _testParty.Id;
            _testUpdatedParty.Metadata.Clear();
            _testUpdatedParty.Metadata.Add(_testNewMetadata);

            _mockTransaction = new Mock<ITransaction>(MockBehavior.Strict);
            _mockMemoryStoreClient = new Mock<IMemoryStoreClient>(MockBehavior.Strict);
            _mockMemoryStoreClient.Setup(client => client.Dispose()).Verifiable();
            _mockMemoryStoreClient.Setup(client => client.CreateTransaction()).Returns(_mockTransaction.Object);
            _mockAnalyticsSender = new Mock<IAnalyticsSender>(MockBehavior.Strict);

            var memoryStoreClientManager = new Mock<IMemoryStoreClientManager<IMemoryStoreClient>>(MockBehavior.Strict);
            memoryStoreClientManager.Setup(manager => manager.GetClient()).Returns(_mockMemoryStoreClient.Object);
            _partyService = new PartyServiceImpl(memoryStoreClientManager.Object, _mockAnalyticsSender.Object);
        }

        [Test]
        public void ReturnInvalidArgumentWhenNoUpdatedPartyIsProvided()
        {
            // Check that having a non-empty updated party is enforced by UpdatePartyInfo.
            var context = Util.CreateFakeCallContext(TestPlayerId, Pit);
            var exception =
                Assert.ThrowsAsync<RpcException>(() => _partyService.UpdateParty(new UpdatePartyRequest(), context));
            Assert.That(exception.Message, Contains.Substring("requires a non-empty updated party"));
            Assert.AreEqual(StatusCode.InvalidArgument, exception.StatusCode);
        }

        [Test]
        public void ReturnInvalidArgumentWhenTheUpdatedPartyHasAnEmptyId()
        {
            // Check that having a non-empty id on the updated party is enforced by UpdatePartyInfo.
            var context = Util.CreateFakeCallContext(TestPlayerId, Pit);
            var missingPartyIdRequest = new UpdatePartyRequest { UpdatedParty = new PartyProto() };
            var exception =
                Assert.ThrowsAsync<RpcException>(() => _partyService.UpdateParty(missingPartyIdRequest, context));
            Assert.That(exception.Message, Contains.Substring("requires an updated party with a non-empty id"));
            Assert.AreEqual(StatusCode.InvalidArgument, exception.StatusCode);
        }

        [Test]
        public void ReturnNotFoundWhenNoPartyWithTheGivenIdExists()
        {
            // Setup the client such that it will claim there are no parties with the given id.
            _mockMemoryStoreClient.Setup(client => client.GetAsync<PartyDataModel>(_testUpdatedParty.Id))
                .ReturnsAsync((PartyDataModel) null);

            // Check that an exception was thrown signaling that the update operation failed.
            var context = Util.CreateFakeCallContext(TestPlayerId, Pit);
            var request = new UpdatePartyRequest { UpdatedParty = _testUpdatedParty };
            var exception = Assert.ThrowsAsync<RpcException>(() => _partyService.UpdateParty(request, context));
            Assert.That(exception.Message, Contains.Substring("no such party with the given id"));
            Assert.AreEqual(StatusCode.NotFound, exception.StatusCode);
        }

        [Test]
        public void ReturnPermissionDeniedWhenPlayerIsNotTheLeader()
        {
            // Setup the client such that it will claim that the party has as leader a different player other than the
            // one making the request.
            var party = new PartyDataModel(_testParty);
            party.UpdatePartyLeader(TestPlayerId2);

            _mockMemoryStoreClient.Setup(client => client.GetAsync<PartyDataModel>(_testUpdatedParty.Id)).ReturnsAsync(party);

            // Check that an exception was thrown signaling that the update operation failed.
            var context = Util.CreateFakeCallContext(TestPlayerId, Pit);
            var request = new UpdatePartyRequest { UpdatedParty = _testUpdatedParty };
            var exception = Assert.ThrowsAsync<RpcException>(() => _partyService.UpdateParty(request, context));
            Assert.That(exception.Message, Contains.Substring("can only be done by the leader"));
            Assert.AreEqual(StatusCode.PermissionDenied, exception.StatusCode);
        }

        [Test]
        public void ReturnFailedPreconditionWhenNewLeaderIsNotAMemberOfTheParty()
        {
            // Setup the client such that it will claim that the party doesn't have as member the newly proposed leader.
            var party = new PartyDataModel(_testParty);
            party.MemberIdToPit.Remove(TestPlayerId2);

            _mockMemoryStoreClient.Setup(client => client.GetAsync<PartyDataModel>(_testUpdatedParty.Id)).ReturnsAsync(party);

            // Check that an exception was thrown signaling that the update operation failed.
            var context = Util.CreateFakeCallContext(TestPlayerId, Pit);
            var request = new UpdatePartyRequest { UpdatedParty = _testUpdatedParty };
            var exception = Assert.ThrowsAsync<RpcException>(() => _partyService.UpdateParty(request, context));
            Assert.That(exception.Message, Contains.Substring("new leader is not a member"));
            Assert.AreEqual(StatusCode.FailedPrecondition, exception.StatusCode);
        }

        [Test]
        public void ReturnFailedPreconditionIfEncounteringErrorWhileUpdatingMemberLimits()
        {
            _mockMemoryStoreClient.Setup(client => client.GetAsync<PartyDataModel>(_testUpdatedParty.Id))
                .ReturnsAsync(_testParty);

            // Perform a request where the updated value of maxMembers is less than the current amount of members.
            var updatedParty = new PartyProto(_testUpdatedParty) { MinMembers = 1, MaxMembers = 1 };
            var request = new UpdatePartyRequest { UpdatedParty = updatedParty };

            // Check that an exception was thrown signaling that the update operation failed.
            var context = Util.CreateFakeCallContext(TestPlayerId, Pit);
            var exception = Assert.ThrowsAsync<RpcException>(() => _partyService.UpdateParty(request, context));
            Assert.That(exception.Message, Contains.Substring("error while updating the minimum and maximum"));
            Assert.AreEqual(StatusCode.FailedPrecondition, exception.StatusCode);
        }

        [Test]
        public void ReturnUpdatedPartySuccessfullyWhenAdequateInformationIsProvided()
        {
            // Setup the client such that it will successfully complete the operation.
            var updatedEntries = new List<Entry>();
            _mockMemoryStoreClient.Setup(client => client.GetAsync<PartyDataModel>(_testUpdatedParty.Id))
                .ReturnsAsync(_testParty);
            _mockTransaction.Setup(tr => tr.UpdateAll(It.IsAny<IEnumerable<Entry>>()))
                .Callback<IEnumerable<Entry>>(entries => updatedEntries.AddRange(entries));
            _mockTransaction.Setup(tr => tr.Dispose());
            _mockAnalyticsSender.Setup(
                sender => sender.Send(AnalyticsConstants.PartyClass, AnalyticsEventType, 
                    It.Is<Dictionary<string, object>>(d => AnalyticsAttributesExpectations(d)), TestPlayerId));

            // Check that the operation has completed successfully.
            var context = Util.CreateFakeCallContext(TestPlayerId, Pit);
            var request = new UpdatePartyRequest { UpdatedParty = _testUpdatedParty };
            var returnedParty = _partyService.UpdateParty(request, context).Result.Party;
            Assert.IsNotNull(returnedParty);
            Assert.AreEqual(_testParty.Id, returnedParty.Id);
            Assert.AreEqual(TestPlayerId2, returnedParty.LeaderPlayerId);
            Assert.AreEqual(TestNewMinMembers, returnedParty.MinMembers);
            Assert.AreEqual(TestNewMaxMembers, returnedParty.MaxMembers);
            Assert.AreEqual(_testUpdatedParty.CurrentPhase, returnedParty.CurrentPhase);
            CollectionAssert.AreEquivalent(new Dictionary<string, string> { { "enemy", "Dumbledore" } },
                returnedParty.Metadata);

            // Verify that the expected party was sent for update.
            Assert.AreEqual(1, updatedEntries.Count);

            var party = (PartyDataModel) updatedEntries[0];
            Assert.AreEqual(_testParty.Id, party.Id);
            Assert.AreEqual(TestPlayerId2, party.LeaderPlayerId);
            Assert.AreEqual(TestNewMinMembers, party.MinMembers);
            Assert.AreEqual(TestNewMaxMembers, party.MaxMembers);
            Assert.AreEqual(PartyDataModel.Phase.Matchmaking, party.CurrentPhase);
            CollectionAssert.AreEquivalent(new Dictionary<string, string> { { "enemy", "Dumbledore" } }, party.Metadata);

            _mockAnalyticsSender.VerifyAll();
        }

        private bool AnalyticsAttributesExpectations(Dictionary<string, object> dictionary)
        {
            return dictionary[AnalyticsConstants.PlayerId] is string playerId && playerId == TestPlayerId &&
                   dictionary[AnalyticsConstants.PartyId] is string partyId && partyId == _testParty.Id &&
                   dictionary[AnalyticsNewPartyState] is Dictionary<string, object> newState &&
                   newState[AnalyticsPartyLeaderId] is string partyLeaderId && partyLeaderId == TestPlayerId2 &&
                   newState[AnalyticsMaxMembers] is uint maxMembers && maxMembers == TestNewMaxMembers &&
                   newState[AnalyticsMinMembers] is uint minMembers && minMembers == TestNewMinMembers;
        }
    }
}
