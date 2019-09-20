using System;
using System.Collections.Generic;
using Grpc.Core;
using Improbable.OnlineServices.DataModel;
using Improbable.OnlineServices.DataModel.Gateway;
using Improbable.OnlineServices.Proto.Gateway;
using MemoryStore;
using Moq;
using NUnit.Framework;
using Serilog;
using Serilog.Events;
using PartyDataModel = Improbable.OnlineServices.DataModel.Party.Party;
using PartyPhaseDataModel = Improbable.OnlineServices.DataModel.Party.Party.Phase;
using PartyProto = Improbable.OnlineServices.Proto.Party.Party;
using PartyPhaseProto = Improbable.OnlineServices.Proto.Party.Party.Types.Phase;

namespace GatewayInternal.Test
{
    [TestFixture]
    public class AssignDeploymentsShould
    {
        private const string LeaderPartyMatched = "LeaderMatched";
        private const string PlayerPartyMatched = "PlayerMatched";
        private const string LeaderPartyError = "LeaderError";
        private const string LeaderPartyRequeue = "LeaderRequeue";
        private const string Pit = "Pit";

        private PartyDataModel _partyMatched;
        private PartyDataModel _partyError;
        private PartyDataModel _partyRequeue;

        private PartyJoinRequest _matchedPartyJoinRequest;
        private PartyJoinRequest _errorPartyJoinRequest;
        private PartyJoinRequest _requeuePartyJoinRequest;

        private Mock<ILogger> _logger;
        private Mock<ITransaction> _transaction;
        private Mock<IMemoryStoreClient> _memoryStoreClient;
        private GatewayInternalServiceImpl _service;

        [SetUp]
        public void Setup()
        {
            _partyMatched = new PartyDataModel(LeaderPartyMatched, Pit);
            _partyMatched.AddPlayerToParty(PlayerPartyMatched, Pit);
            _matchedPartyJoinRequest = new PartyJoinRequest(_partyMatched, "type-matched", null);
            _partyError = new PartyDataModel(LeaderPartyError, Pit);
            _errorPartyJoinRequest = new PartyJoinRequest(_partyError, "type-error", null);
            _partyRequeue = new PartyDataModel(LeaderPartyRequeue, Pit);
            _requeuePartyJoinRequest = new PartyJoinRequest(_partyRequeue, "type-requeue", null);

            _logger = new Mock<ILogger>();
            Log.Logger = _logger.Object;

            _transaction = new Mock<ITransaction>(MockBehavior.Strict);
            _transaction.Setup(tx => tx.Dispose());

            _memoryStoreClient = new Mock<IMemoryStoreClient>(MockBehavior.Strict);
            _memoryStoreClient.Setup(client => client.Dispose());
            _memoryStoreClient.Setup(client => client.CreateTransaction()).Returns(_transaction.Object);

            var memoryStoreClientManager = new Mock<IMemoryStoreClientManager<IMemoryStoreClient>>();
            memoryStoreClientManager.Setup(manager => manager.GetClient()).Returns(_memoryStoreClient.Object);
            _service = new GatewayInternalServiceImpl(memoryStoreClientManager.Object);
        }

        [Test]
        public void CallAppropriateMemoryStoreMethods()
        {
            var startTime = (DateTime.UtcNow - DateTime.UnixEpoch).TotalMilliseconds;
            var updated = new List<Entry>();
            var requeued = new List<QueuedEntry>();
            var deleted = new List<Entry>();
            _memoryStoreClient
                .Setup(client => client.GetAsync<PartyJoinRequest>(_partyMatched.Id)).ReturnsAsync(_matchedPartyJoinRequest);
            _memoryStoreClient
                .Setup(client => client.GetAsync<PartyJoinRequest>(_partyError.Id)).ReturnsAsync(_errorPartyJoinRequest);
            _memoryStoreClient
                .Setup(client => client.GetAsync<PartyJoinRequest>(_requeuePartyJoinRequest.Id))
                .ReturnsAsync(_requeuePartyJoinRequest);
            _memoryStoreClient
                .Setup(client => client.GetAsync<PlayerJoinRequest>(It.IsAny<string>()))
                .ReturnsAsync((string id) => new PlayerJoinRequest(id, "", "", "", "", null) { State = MatchState.Matching });
            _transaction.Setup(tx => tx.UpdateAll(It.IsAny<IEnumerable<Entry>>()))
                .Callback<IEnumerable<Entry>>(reqs => updated.AddRange(reqs));
            _transaction.Setup(tx => tx.EnqueueAll(It.IsAny<IEnumerable<QueuedEntry>>()))
                .Callback<IEnumerable<QueuedEntry>>(reqs => requeued.AddRange(reqs));
            _transaction.Setup(tx => tx.DeleteAll(It.IsAny<IEnumerable<Entry>>()))
                .Callback<IEnumerable<Entry>>(reqs => deleted.AddRange(reqs));

            var ctx = Util.CreateFakeCallContext();
            var req = new AssignDeploymentsRequest
            {
                Assignments =
                {
                    new Assignment
                    {
                        Party = ConvertToProto(_partyMatched),
                        Result = Assignment.Types.Result.Matched,
                        DeploymentId = "1",
                        DeploymentName = "deployment1"
                    },
                    new Assignment
                    {
                        Party = ConvertToProto(_partyError),
                        Result = Assignment.Types.Result.Error
                    },
                    new Assignment
                    {
                        Party = ConvertToProto(_partyRequeue),
                        Result = Assignment.Types.Result.Requeued
                    }
                }
            };

            _service.AssignDeployments(req, ctx);
            Assert.AreEqual(StatusCode.OK, ctx.Status.StatusCode);
            _memoryStoreClient.Verify(client => client.GetAsync<PlayerJoinRequest>(It.IsAny<string>()), Times.Exactly(4));

            // We expect 6 entities to be updates. First 5 should be PlayerJoinRequests. The last should be the
            // PartyJoinRequest of the requeued party.
            Assert.AreEqual(5, updated.Count);
            var playerJoinRequests = new List<PlayerJoinRequest>();
            for (var i = 0; i < 4; i++)
            {
                Assert.IsInstanceOf<PlayerJoinRequest>(updated[i]);
                playerJoinRequests.Add((PlayerJoinRequest) updated[i]);
            }

            Assert.AreEqual(LeaderPartyMatched, playerJoinRequests[0].Id);
            Assert.AreEqual("1", playerJoinRequests[0].DeploymentId);
            Assert.AreEqual("deployment1", playerJoinRequests[0].DeploymentName);
            Assert.AreEqual(MatchState.Matched, playerJoinRequests[0].State);
            Assert.AreEqual(PlayerPartyMatched, playerJoinRequests[1].PlayerIdentity);
            Assert.AreEqual("1", playerJoinRequests[1].DeploymentId);
            Assert.AreEqual("deployment1", playerJoinRequests[1].DeploymentName);
            Assert.AreEqual(MatchState.Matched, playerJoinRequests[1].State);

            Assert.AreEqual(LeaderPartyError, playerJoinRequests[2].PlayerIdentity);
            Assert.AreEqual(MatchState.Error, playerJoinRequests[2].State);

            Assert.AreEqual(LeaderPartyRequeue, playerJoinRequests[3].PlayerIdentity);
            Assert.AreEqual(MatchState.Requested, playerJoinRequests[3].State);

            Assert.IsInstanceOf<PartyJoinRequest>(updated[4]);
            var updatedPartyRequest = (PartyJoinRequest) updated[4];
            Assert.AreEqual(_partyRequeue.Id, updatedPartyRequest.Id);
            Assert.AreEqual("type-requeue", updatedPartyRequest.Type);
            Assert.AreEqual("type-requeue", updatedPartyRequest.QueueName);
            Assert.AreEqual("type-requeue", updatedPartyRequest.QueueName);
            Assert.Greater((DateTime.UtcNow - DateTime.UnixEpoch).TotalMilliseconds, updatedPartyRequest.Score);
            Assert.Greater(updatedPartyRequest.Score, startTime);

            // We expect for the requeued PartyJoinRequest to be exactly the same as the updated one.
            Assert.AreEqual(1, requeued.Count);
            Assert.AreEqual(updatedPartyRequest, requeued[0]);

            // We expect for 2 PartyJoinRequests to have been deleted, one per each party which has reached a final
            // state.
            Assert.AreEqual(2, deleted.Count);
            Assert.AreEqual(_partyMatched.Id, deleted[0].Id);
            Assert.AreEqual(_partyError.Id, deleted[1].Id);

            _logger.Verify(logger => logger.Warning(It.IsAny<string>()), Times.Never);
        }

        [Test]
        public void ContinueWithoutAssignmentIfJoinRequestHasBeenDeleted()
        {
            _memoryStoreClient
                .Setup(client => client.GetAsync<PlayerJoinRequest>(It.IsAny<string>()))
                .ReturnsAsync((PlayerJoinRequest) null);
            _memoryStoreClient
                .Setup(client => client.GetAsync<PartyJoinRequest>(_partyMatched.Id))
                .ReturnsAsync((PartyJoinRequest) null);

            var updated = new List<Entry>();
            var requeued = new List<QueuedEntry>();
            var deleted = new List<Entry>();
            _transaction.Setup(tx => tx.UpdateAll(It.IsAny<IEnumerable<Entry>>()))
                .Callback<IEnumerable<Entry>>(reqs => updated.AddRange(reqs));
            _transaction.Setup(tx => tx.EnqueueAll(It.IsAny<IEnumerable<QueuedEntry>>()))
                .Callback<IEnumerable<QueuedEntry>>(reqs => requeued.AddRange(reqs));
            _transaction.Setup(tx => tx.DeleteAll(It.IsAny<IEnumerable<Entry>>()))
                .Callback<IEnumerable<Entry>>(reqs => deleted.AddRange(reqs));

            var ctx = Util.CreateFakeCallContext();
            var req = new AssignDeploymentsRequest
            {
                Assignments =
                {
                    new Assignment
                    {
                        Party = ConvertToProto(_partyMatched),
                        Result = Assignment.Types.Result.Matched
                    }
                }
            };
            var task = _service.AssignDeployments(req, ctx);
            Assert.That(task.IsCompleted);
            Assert.AreEqual(StatusCode.OK, ctx.Status.StatusCode);
            Assert.IsEmpty(updated);
            Assert.IsEmpty(deleted);
            Assert.IsEmpty(requeued);
        }

        [Test]
        public void LogWarningIfTransactionAborted()
        {
            _memoryStoreClient.Setup(client => client.GetAsync<PlayerJoinRequest>(LeaderPartyRequeue))
                .ReturnsAsync(new PlayerJoinRequest(LeaderPartyRequeue, "", "", "", "", null));
            _memoryStoreClient.Setup(client => client.GetAsync<PartyJoinRequest>(_partyRequeue.Id))
                .ReturnsAsync(_requeuePartyJoinRequest);
            _transaction.Setup(tran => tran.UpdateAll(It.IsAny<IEnumerable<Entry>>()));
            _transaction.Setup(tran => tran.DeleteAll(It.IsAny<IEnumerable<Entry>>()));
            _transaction.Setup(tran => tran.EnqueueAll(It.IsAny<IEnumerable<QueuedEntry>>()));
            _transaction.Setup(tran => tran.Dispose()).Throws<TransactionAbortedException>();

            var ctx = Util.CreateFakeCallContext();
            var exception = Assert.ThrowsAsync<RpcException>(() => _service.AssignDeployments(new AssignDeploymentsRequest
            {
                Assignments =
                {
                    new Assignment
                    {
                        Party = ConvertToProto(_partyRequeue),
                        Result = Assignment.Types.Result.Requeued
                    }
                }
            }, ctx));

            Assert.AreEqual(StatusCode.Unavailable, exception.StatusCode);
            _logger.Verify(logger => logger.Write(LogEventLevel.Warning, It.IsRegex("^Transaction aborted")),
                Times.Once);
        }

        private static PartyProto ConvertToProto(PartyDataModel party)
        {
            return new PartyProto
            {
                Id = party.Id,
                LeaderPlayerId = party.LeaderPlayerId,
                MinMembers = party.MinMembers,
                MaxMembers = party.MaxMembers,
                Metadata = { party.Metadata },
                MemberIds = { party.MemberIds },
                CurrentPhase = ConvertToProto(party.CurrentPhase)
            };
        }

        private static PartyPhaseProto ConvertToProto(PartyPhaseDataModel phase)
        {
            switch (phase)
            {
                case PartyPhaseDataModel.Forming:
                    return PartyPhaseProto.Forming;
                case PartyPhaseDataModel.Matchmaking:
                    return PartyPhaseProto.Matchmaking;
                case PartyPhaseDataModel.InGame:
                    return PartyPhaseProto.InGame;
                default:
                    return PartyPhaseProto.Unknown;
            }
        }
    }
}
