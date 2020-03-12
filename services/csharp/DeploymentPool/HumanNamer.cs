using System;
using System.Collections.Generic;

namespace DeploymentPool
{
    public class HumanNamer
    {
        private static string[] wordList =
        {
            "ack", "alabama", "alanine", "alaska", "alpha", "angel", "apart",
            "april", "arizona", "arkansas", "artist", "aspire", "aspen",
            "august", "autumn", "avocado", "bacon", "bakerloo", "batman",
            "beer", "berlin", "berry", "black", "blossom", "blue", "bluebird",
            "bravo", "bulldog", "burger", "butter", "cali", "carbon", "cardinal",
            "carolina", "carpet", "cat", "ceiling", "charlie", "chicken",
            "coffee", "cola", "cold", "colorado", "comet", "connect", "crazy",
            "cup", "dakota", "december", "delaware", "delta", "diet", "don",
            "double", "early", "earth", "east", "echo", "edward", "eight",
            "eighteen", "eleven", "emma", "enemy", "equal", "failed", "fanta",
            "fifteen", "fillet", "finch", "fish", "five", "fix", "floor",
            "florida", "football", "four", "fourteen", "foxtrot", "freddie",
            "friend", "fruit", "gee", "georgia", "glucose", "golf", "green",
            "grey", "hamper", "happy", "harry", "hawaii", "helium", "high",
            "hot", "hotel", "hydrogen", "idaho", "illinois", "india", "indigo",
            "ink", "iowa", "island", "item", "jersey", "jig", "johnny", "juliet",
            "july", "jupiter", "kansas", "kentucky", "kilo", "king", "kitten",
            "lactose", "lake", "lamp", "lemon", "leopard", "lima", "lion",
            "lithium", "london", "louise", "low", "magazine", "magnet", "maine",
            "mango", "march", "mars", "maryland", "massive", "may", "mexico",
            "michigan", "mike", "minnie", "mirror", "missing", "missouri",
            "mancer", "mocking", "monkey", "montana", "moon", "mountain",
            "muppet", "music", "nebraska", "neptune", "network", "nevada",
            "nine", "nineteen", "nitrogen", "north", "november", "nuts",
            "october", "ohio", "oklahoma", "one", "orange", "oranges", "oregon",
            "oscar", "oven", "oxygen", "papa", "paris", "pasta", "pencil",
            "pip", "pizza", "pluto", "potato", "princess", "purple", "quebec",
            "queen", "quiet", "red", "river", "robert", "robin", "romeo",
            "rugby", "sad", "salami", "saturn", "september", "seven", "seventy",
            "shade", "sierra", "single", "sink", "six", "sixteen", "skylark",
            "snake", "social", "sodium", "solar", "south", "speaker", "spring",
            "stairway", "steak", "stream", "summer", "sweet", "table", "tango",
            "ten", "tennis", "texas", "thirteen", "three", "timing", "triple",
            "twelve", "twenty", "two", "uncle", "undress", "uniform", "uranus",
            "utah", "vegan", "venus", "vermont", "victor", "video", "violet",
            "virginia", "west", "whiskey", "white", "william", "winner",
            "winter", "wolfram", "wyoming", "xray", "yankee", "yellow", "zebra",
            "zulu"
        };

        private static readonly Random random = new Random();

        public static string GetRandomName(int words, string separator)
        {
            var selectedWords = new List<string>();
            for (int i = 0; i < words; i++)
            {
                selectedWords.Add(wordList[random.Next(wordList.Length)]);
            }

            return string.Join(separator, selectedWords);
        }
    }
}
