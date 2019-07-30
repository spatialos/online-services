using System;
using System.Collections.Generic;
using NUnit.Framework;
using PartyDataModel = Improbable.OnlineServices.DataModel.Party.Party;

namespace Improbable.OnlineServices.DataModel.Test.Party
{
    [TestFixture]
    public class ConstructingPartyShould
    {
        private const string TestLeaderPlayerId = "IAmLeaderWoo";
        private const string TestPit = "IAmPitWoo";
        private const uint TestMinMembers = 100;
        private const uint TestMaxMembers = 200;

        private static readonly IDictionary<string, string> _testMetadata = new Dictionary<string, string>
        {
            {"WhatIsMyPurpose", "PassTheMetadata"}
        };

        private static readonly IDictionary<string, string> _testMemberIdToPit = new Dictionary<string, string>
        {
            {TestLeaderPlayerId, TestPit}
        };

        [TearDown]
        public void TearDown()
        {
            PartyDataModel.Defaults.MinMembers = 1;
            PartyDataModel.Defaults.MaxMembers = uint.MaxValue;
        }

        [Test]
        public void ThrowArgumentExceptionIfMinMembersIsGreaterThanMaxMembers()
        {
            Assert.Throws<ArgumentException>(() => new PartyDataModel(TestLeaderPlayerId, TestPit, 20, 10));
        }

        [Test]
        public void SetAllFieldsWhenGiven()
        {
            var party = new PartyDataModel(TestLeaderPlayerId, TestPit, TestMinMembers, TestMaxMembers, _testMetadata);

            Assert.False(string.IsNullOrEmpty(party.Id));
            Assert.AreEqual(TestLeaderPlayerId, party.LeaderPlayerId);
            Assert.AreEqual(TestMinMembers, party.MinMembers);
            Assert.AreEqual(TestMaxMembers, party.MaxMembers);
            Assert.AreEqual(PartyDataModel.Phase.Forming, party.CurrentPhase);
            CollectionAssert.AreEquivalent(_testMetadata, party.Metadata);
            CollectionAssert.AreEquivalent(_testMemberIdToPit, party.MemberIdToPit);
            Assert.IsNull(party.PreviousState);
        }

        [Test]
        public void SetDefaultValuesWhenParametersAreAbsent()
        {
            PartyDataModel.Defaults.MinMembers = 10;
            PartyDataModel.Defaults.MaxMembers = 100;
            var party = new PartyDataModel(TestLeaderPlayerId, TestPit);

            Assert.False(string.IsNullOrEmpty(party.Id));
            Assert.AreEqual(TestLeaderPlayerId, party.LeaderPlayerId);
            Assert.AreEqual(10, party.MinMembers);
            Assert.AreEqual(100, party.MaxMembers);
            Assert.AreEqual(PartyDataModel.Phase.Forming, party.CurrentPhase);
            CollectionAssert.IsEmpty(party.Metadata);
            CollectionAssert.AreEquivalent(_testMemberIdToPit, party.MemberIdToPit);
            Assert.IsNull(party.PreviousState);
        }

        [Test]
        public void ThrowExceptionIfMinMembersIsGreaterThanMaxMembers()
        {
            var exception =
                Assert.Throws<ArgumentException>(() => new PartyDataModel(TestLeaderPlayerId, TestPit, 10, 5));
            Assert.That(exception.Message, Contains.Substring("minimum number of members cannot be higher"));
        }
    }
}
