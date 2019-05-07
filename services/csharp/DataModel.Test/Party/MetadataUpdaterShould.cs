using System.Collections.Generic;
using Improbable.OnlineServices.DataModel.Party;
using NUnit.Framework;
using PartyDataModel = Improbable.OnlineServices.DataModel.Party.Party;

namespace Improbable.OnlineServices.DataModel.Test.Party
{
    [TestFixture]
    public class MetadataUpdateMetadataShould
    {
        private IDictionary<string, string> _metadata;

        [SetUp]
        public void SetUp()
        {
            _metadata = new Dictionary<string, string>
            {
                {"MerlinGuess", "Michael"},
                {"NumFailedMissions", "2"}
            };
        }

        [Test]
        public void DeleteMetadataIfValueIsEmptyOrNull()
        {
            var updates = new Dictionary<string, string>
            {
                {"MerlinGuess", null},
                {"NumFailedMissions", ""}
            };
            MetadataUpdater.Update(_metadata, updates);
            CollectionAssert.IsEmpty(_metadata);
        }

        [Test]
        public void UpdateMetadataIfValueIsNotEmptyOrNull()
        {
            var updates = new Dictionary<string, string>
            {
                {"NumFailedMissions", "1"},
                {"PercivalGuess", "Andrea"}
            };
            MetadataUpdater.Update(_metadata, updates);
            CollectionAssert.AreEquivalent(new Dictionary<string, string>
            {
                {"MerlinGuess", "Michael"},
                {"NumFailedMissions", "1"},
                {"PercivalGuess", "Andrea"},
            }, _metadata);
        }
    }
}