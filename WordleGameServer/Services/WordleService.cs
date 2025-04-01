using Grpc.Core;
using WordleGameServer.Protos;
using WordServer.Protos;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Newtonsoft.Json;
using System.IO;

namespace WordleServer.Services
{
    public class WordleService : DailyWordle.DailyWordleBase
    {
        private readonly ILogger<WordleService> _logger;
        private readonly DailyWord.DailyWordClient _wordClient;
        private static readonly ConcurrentDictionary<string, List<string>> _playerGuesses = new();
        private readonly Dictionary<string, List<string>> playerGuesses = new();
        private const string StatsFile = "wordle_stats.json";

        public WordleService(ILogger<WordleService> logger, DailyWord.DailyWordClient wordClient)
        {
            _logger = logger;
            _wordClient = wordClient;
        }

        public override async Task Play(
            IAsyncStreamReader<PlayRequest> requestStream,
            IServerStreamWriter<PlayReply> responseStream,
            ServerCallContext context)
        {
            var wordOfTheDayResponse = await _wordClient.GetWordAsync(new WordRequest());
            string wordOfTheDay = wordOfTheDayResponse.Word.ToUpper();
            Console.WriteLine(wordOfTheDay);

            var playerId = context.Peer;
            if (!playerGuesses.ContainsKey(playerId))
            {
                playerGuesses[playerId] = new List<string>();
            }

            HashSet<char> includedLetters = new();
            HashSet<char> excludedLetters = new();
            HashSet<char> availableLetters = new("ABCDEFGHIJKLMNOPQRSTUVWXYZ");

            int attempts = 0;

            await foreach (var request in requestStream.ReadAllAsync())
            {
                string guess = request.Guess.ToUpper().Trim();

                if (playerGuesses[playerId].Contains(guess))
                {
                    await responseStream.WriteAsync(new PlayReply { Answer = "You already guessed that word!" });
                    attempts--;
                    continue;
                }

                var validationResponse = await _wordClient.ValidateWordAsync(new WordInput { Word = guess });
                if (!validationResponse.IsValid_)
                {
                    await responseStream.WriteAsync(new PlayReply { Answer = "Invalid word!" });
                    attempts--;
                    continue;
                }

                playerGuesses[playerId].Add(guess);
                attempts++;

                string feedback = GenerateFeedback(wordOfTheDay, guess, includedLetters, excludedLetters);
                foreach (char c in guess)
                {
                    availableLetters.Remove(c);
                }

                string responseMessage = $"\n({attempts}): {guess}\n     {feedback}\n" +
                                         $"     Included:  {string.Join(", ", includedLetters)}\n" +
                                         $"     Available: {string.Join(", ", availableLetters)}\n" +
                                         $"     Excluded:  {string.Join(", ", excludedLetters)}";

                await responseStream.WriteAsync(new PlayReply { Answer = responseMessage });

                if (guess == wordOfTheDay)
                {
                    await responseStream.WriteAsync(new PlayReply { Answer = "\nYou win!" });
                    UpdateStats(true, attempts);
                    await DisplayStats(responseStream);
                    playerGuesses.Remove(playerId, out _);
                    return;
                }

                if (attempts >= 6)
                {
                    await responseStream.WriteAsync(new PlayReply { Answer = $"\nGame Over! The word was: {wordOfTheDay}" });
                    UpdateStats(false, attempts);
                    await DisplayStats(responseStream);
                    break;
                }
            }
        }

        private void UpdateStats(bool isWinner, int attempts)
        {
            var stats = LoadStats();
            stats.Players++;
            if (isWinner) stats.Winners++;
            stats.TotalGuesses += attempts;
            File.WriteAllText(StatsFile, JsonConvert.SerializeObject(stats));
        }

        private WordleStats LoadStats()
        {
            if (File.Exists(StatsFile))
            {
                return JsonConvert.DeserializeObject<WordleStats>(File.ReadAllText(StatsFile)) ?? new WordleStats();
            }
            return new WordleStats();
        }

        private async Task DisplayStats(IServerStreamWriter<PlayReply> responseStream)
        {
            var stats = LoadStats();
            string statsMessage = $"\nGame Statistics:\n" +
                                  $"Total Players: {stats.Players}\n" +
                                  $"Winners: {stats.Winners}\n" +
                                  $"Total Guesses: {stats.TotalGuesses}\n" +
                                  $"Average Guesses per Player: {stats.AverageGuesses:F2}";

            await responseStream.WriteAsync(new PlayReply { Answer = statsMessage });
        }

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
                int letterCountInWord = wordOfTheDay.Count(c => c == letter);

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

    public class WordleStats
    {
        public int Players { get; set; } = 0;
        public int Winners { get; set; } = 0;
        public int TotalGuesses { get; set; } = 0;
        public double AverageGuesses => Players > 0 ? (double)TotalGuesses / Players : 0;
    }
}