using Grpc.Core;
using WordleGameServer.Protos;
using WordServer.Protos;
using System.Collections.Concurrent;

namespace WordleServer.Services
{
    public class WordleService : DailyWordle.DailyWordleBase
    {
        private readonly ILogger<WordleService> _logger;
        private readonly DailyWord.DailyWordClient _wordClient;
        private static readonly ConcurrentDictionary<string, List<string>> _playerGuesses = new();
        private readonly Dictionary<string, List<string>> playerGuesses = new();


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
                string guess = request.Guess.ToUpper();

                if (playerGuesses[playerId].Contains(guess))
                {
                    await responseStream.WriteAsync(new PlayReply { Answer = "You already guessed that word!" });
                    continue;
                }

                var validationResponse = await _wordClient.ValidateWordAsync(new WordInput { Word = guess });
                if (!validationResponse.IsValid_)
                {
                    await responseStream.WriteAsync(new PlayReply { Answer = "Invalid word!" });
                    continue;
                }

                playerGuesses[playerId].Add(guess);
                attempts++;

                string feedback = GenerateFeedback(wordOfTheDay, guess, includedLetters, excludedLetters);
                foreach (char c in guess)
                {
                    availableLetters.Remove(c);
                }

                string includedStr = includedLetters.Count > 0 ? string.Join(", ", includedLetters) : "None";
                string availableStr = availableLetters.Count > 0 ? string.Join(", ", availableLetters) : "None";
                string excludedStr = excludedLetters.Count > 0 ? string.Join(", ", excludedLetters) : "None";

                string responseMessage = $"\n({attempts}): {guess}\n     {feedback}\n\n" +
                                         $"     Included:  {includedStr}\n" +
                                         $"     Available: {availableStr}\n" +
                                         $"     Excluded:  {excludedStr}";

                await responseStream.WriteAsync(new PlayReply { Answer = responseMessage });

                if (guess == wordOfTheDay)
                {
                    await responseStream.WriteAsync(new PlayReply { Answer = "\nYou win!" });
                    playerGuesses.Remove(playerId, out _);
                    break;
                }

                if (attempts >= 6)
                {
                    await responseStream.WriteAsync(new PlayReply { Answer = $"\nGame Over! The word was: {wordOfTheDay}" });
                    break;
                }
            }
        }


        private string GenerateFeedback(string wordOfTheDay, string guess, HashSet<char> included, HashSet<char> excluded)
        {
            char[] feedback = new char[5];
            bool[] used = new bool[5];

            for (int i = 0; i < 5; i++)
            {
                if (guess[i] == wordOfTheDay[i])
                {
                    feedback[i] = '*';
                    used[i] = true;
                    included.Add(guess[i]);
                }
                else
                {
                    feedback[i] = 'x';
                }
            }

            // Mark misplaced letters ('?')
            for (int i = 0; i < 5; i++)
            {
                if (feedback[i] == '*') continue;

                for (int j = 0; j < 5; j++)
                {
                    if (!used[j] && guess[i] == wordOfTheDay[j])
                    {
                        feedback[i] = '?';
                        used[j] = true;
                        included.Add(guess[i]);
                        break;
                    }
                }

                if (feedback[i] == 'x')
                {
                    excluded.Add(guess[i]);
                }
            }

            return new string(feedback);
        }

    }
}
