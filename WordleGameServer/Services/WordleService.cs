using Grpc.Core;
using WordleGameServer.Protos;
using System.Collections.Concurrent;
using Newtonsoft.Json;
using System.IO;

namespace WordleServer.Services
{
    public class WordleService : DailyWordle.DailyWordleBase
    {
        private readonly ILogger<WordleService> _logger;
        private static readonly ConcurrentDictionary<string, List<string>> _playerGuesses = new();
        private readonly Dictionary<string, List<string>> playerGuesses = new();
        private const string WordFile = @"C:\Users\profs\source\repos\Wordle Project 22\WordServer\wordle.json"; // Path to the file

        private List<string> _validWords;

        public WordleService(ILogger<WordleService> logger)
        {
            _logger = logger;
            _validWords = LoadValidWords();  // Load valid words on initialization
        }

        // Method to load valid words from the wordle.json file
        private List<string> LoadValidWords()
        {
            if (File.Exists(WordFile))
            {
                var wordData = JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(WordFile));
                return wordData ?? new List<string>();
            }
            return new List<string>();
        }

        public string GetWordOfTheDay()
        {
            // Logic to get Word of the Day remains the same
            if (File.Exists(WordFile))
            {
                var wordData = JsonConvert.DeserializeObject<DailyWordEntry>(File.ReadAllText(WordFile));
                return wordData?.Word ?? "error";
            }
            return "error";
        }

        public override async Task Play(
            IAsyncStreamReader<PlayRequest> requestStream,
            IServerStreamWriter<PlayReply> responseStream,
            ServerCallContext context)
        {
            string wordOfTheDay = GetWordOfTheDay();
            var playerId = context.Peer;

            if (!playerGuesses.ContainsKey(playerId))
            {
                playerGuesses[playerId] = new List<string>();
            }

            HashSet<char> includedLetters = new();
            HashSet<char> excludedLetters = new();
            HashSet<char> availableLetters = new("abcdefghijklmnopqrstuvwxyz");

            // Send the word of the day to the client at the start of the game
            await responseStream.WriteAsync(new PlayReply { WordOfTheDay = wordOfTheDay });

            while (await requestStream.MoveNext())
            {
                string guess = requestStream.Current.Guess.Trim();

                // Check if the guess is a valid word and not already guessed
                if (!IsValidWord(guess))
                {
                    await responseStream.WriteAsync(new PlayReply { Answer = "Invalid word. Please try again." });
                    continue;
                }

                if (playerGuesses[playerId].Contains(guess))
                {
                    await responseStream.WriteAsync(new PlayReply { Answer = "You already guessed that word!" });
                    continue;
                }

                playerGuesses[playerId].Add(guess);

                string feedback = GenerateFeedback(wordOfTheDay, guess, includedLetters, excludedLetters);
                foreach (char c in guess) availableLetters.Remove(c);

                string responseMessage = $"   \n{guess}\n     {feedback}\n" +
                                         $"     Included:  {string.Join(", ", includedLetters)}\n" +
                                         $"     Available: {string.Join(", ", availableLetters)}\n" +
                                         $"     Excluded:  {string.Join(", ", excludedLetters)}";

                await responseStream.WriteAsync(new PlayReply { Answer = responseMessage });
            }
        }

        private bool IsValidWord(string guess)
        {
            // Ensure the guess is a 5-letter word and exists in the valid words list
            return guess.Length == 5 && _validWords.Contains(guess.ToUpper());
        }

        private string GenerateFeedback(string wordOfTheDay, string guess, HashSet<char> included, HashSet<char> excluded)
        {
            char[] feedback = new char[5];
            Dictionary<char, int> matches = new();
            Console.WriteLine($"[DEBUG] Checking win condition: Guess = {guess}, Word = {wordOfTheDay}");

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


    public class DailyWordEntry
    {
        public string Word { get; set; } = "";
        public string Date { get; set; } = "";
    }
}
