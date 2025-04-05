using Grpc.Core;
using WordleGameServer.Protos;
using WordServer.Protos;
using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;

namespace WordleServer.Services
{

/*
 * Name: Logan McCallum Student Number: 1152955 Section: 2
 * Name: Spencer Martin Student Number: 1040415 Section: 2
 * Name: Ashley Burley-Denis Student Number: 0908968 Section: 1
 */

    //WordleService class, inherits DailyWordl.DailyWordleBase,
    public class WordleService : DailyWordle.DailyWordleBase
    {
        private readonly ILogger<WordleService> _logger;
        private static readonly ConcurrentDictionary<string, List<string>> _playerGuesses = new();
        private readonly Dictionary<string, List<string>> playerGuesses = new();
        private readonly DailyWord.DailyWordClient _wordClient;

        private const string StatsFile = "wordle_stats.json";

        //WordleService Constructor
        public WordleService(ILogger<WordleService> logger, DailyWord.DailyWordClient wordClient)
        {
            _logger = logger;
            _wordClient = wordClient;
        }


        //GetWordOfTheDay method, gets the current word of the day.
        public async Task<string> GetWordOfTheDay()
        {
            try
            {
                var request = new WordRequest();
                var reply = await _wordClient.GetWordAsync(request);
                return reply.Word;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching word of the day: {ex.Message}");
                return "ERROR";
            }
        }


        //Play override async method, initializes the game to be played on the client, 
        //response accordingly based on the inputted guesses.
        public override async Task Play(
            IAsyncStreamReader<PlayRequest> requestStream,
            IServerStreamWriter<PlayReply> responseStream,
            ServerCallContext context)
        {
            string wordOfTheDay = await GetWordOfTheDay();
            var playerId = context.Peer;

            if (!playerGuesses.ContainsKey(playerId))
                playerGuesses[playerId] = new List<string>();
            

            HashSet<char> includedLetters = new();
            HashSet<char> excludedLetters = new();
            HashSet<char> availableLetters = new("abcdefghijklmnopqrstuvwxyz");

            // Send the word of the day to the client at the start of the game
            await responseStream.WriteAsync(new PlayReply { WordOfTheDay = wordOfTheDay });
            Console.WriteLine($"Whats the Word: = {wordOfTheDay}");


            int attempts = 0;

            while (await requestStream.MoveNext())
            {
                string guess = requestStream.Current.Guess.Trim();
                var validationReply = await _wordClient.ValidateWordAsync(new WordInput { Word = guess });

                if (playerGuesses[playerId].Contains(guess))
                {
                    await responseStream.WriteAsync(new PlayReply { Answer = "You already guessed that word!" });
                    continue;
                }
                else if (!validationReply.IsValid_)
                {
                    await responseStream.WriteAsync(new PlayReply { Answer = "Invalid word. Try a valid 5-letter word." });
                    continue;
                }
                else
                {
                    attempts++;
                }

                playerGuesses[playerId].Add(guess);


                string feedback = GenerateFeedback(wordOfTheDay, guess, includedLetters, excludedLetters);
                foreach (char c in guess) availableLetters.Remove(c);

                string responseMessage = $"   \n({attempts}) {guess}\n     {feedback}\n" +
                                         $"     Included:  {string.Join(", ", includedLetters)}\n" +
                                         $"     Available: {string.Join(", ", availableLetters)}\n" +
                                         $"     Excluded:  {string.Join(", ", excludedLetters)}";

                await responseStream.WriteAsync(new PlayReply { Answer = responseMessage });

                if (guess.Equals(wordOfTheDay, StringComparison.OrdinalIgnoreCase))
                {
                    UpdateStats(true, attempts);
                    break;
                }
            }

            if (attempts >= 6)
            {
                UpdateStats(false, attempts);
            }
        }

        //UpdateStats method, updates the stats after an
        //attempt of the game, resets the stats if it is a new day.
        private void UpdateStats(bool isWinner, int attempts)
        {
            var stats = LoadStats();
            DateTime currentDate = DateTime.Today;

            //If new day, reset
            if (stats.LastUpdated != currentDate)
            {
                stats.Players = 0;
                stats.Winners = 0;
                stats.TotalGuesses = 0;
                stats.LastUpdated = currentDate;
            }

            stats.Players++;
            if (isWinner) stats.Winners++;
            stats.TotalGuesses += attempts;
            File.WriteAllText(StatsFile, JsonSerializer.Serialize(stats, new JsonSerializerOptions { WriteIndented = true }));
        }

        //LoadStats method, loads the stats in for the current days, wordle
        private WordleStats LoadStats()
        {
            if (File.Exists(StatsFile))
            {
                return JsonSerializer.Deserialize<WordleStats>(File.ReadAllText(StatsFile)) ?? new WordleStats();
            }
            return new WordleStats();
        }


        //GetStats override method,  returns the current stats of the day.
        public override async Task<StatsReply> GetStats(StatsRequest request, ServerCallContext context)
        {
            var stats = LoadStats();
            var reply = new StatsReply
            {
                PlayersCount = stats.Players,
                WinnersPercent = stats.Players > 0 ? (int)((double)stats.Winners / stats.Players * 100) : 0,
                AverageGuesses = stats.Players > 0 ? stats.AverageGuesses : 0
            };

            return await Task.FromResult(reply);
        }

        //GenerateFeedback method, returns a string of what
        //was correctly guessed based on the inputted guess.
        private string GenerateFeedback(string wordOfTheDay, string guess, HashSet<char> included, HashSet<char> excluded)
        {
            char[] feedback = new char[5];
            Dictionary<char, int> matches = new();

            for (int i = 0; i < 5; i++)
            {
                feedback[i] = 'x';
                matches[guess[i]] = 0;
            }

            for (int i = 0; i < 5; i++)
            {
                if (guess[i] == wordOfTheDay[i])
                {
                    feedback[i] = '*';
                    matches[guess[i]]++;
                    included.Add(guess[i]);
                }
            }

            for (int i = 0; i < 5; i++)
            {
                if (feedback[i] == '*') continue;

                char letter = guess[i];
                int letterCountInWord = wordOfTheDay.ToLower().Count(c => c == char.ToLower(letter));

                if (letterCountInWord == 0)
                {
                    feedback[i] = 'x';
                    excluded.Add(letter);
                }
                else if (matches[letter] < letterCountInWord)
                {
                    feedback[i] = '?';
                    matches[letter]++;
                    included.Add(letter);
                }
            }

            return new string(feedback);
        }
    }

    //WordleStats class, holds the stats for the current day.
    public class WordleStats
    {
        public int Players { get; set; } = 0;
        public int Winners { get; set; } = 0;
        public int TotalGuesses { get; set; } = 0;
        public double AverageGuesses => Players > 0 ? (double)TotalGuesses / Players : 0;
        public DateTime LastUpdated { get; set; } = DateTime.MinValue;
    }
}
