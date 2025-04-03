using Grpc.Core;
using WordleGameServer.Protos;
using WordServer.Protos;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace WordleServer.Services
{
    public class WordleService : DailyWordle.DailyWordleBase
    {
        private readonly ILogger<WordleService> _logger;
        private static readonly ConcurrentDictionary<string, List<string>> _playerGuesses = new();
        private readonly Dictionary<string, List<string>> playerGuesses = new();
        private readonly DailyWord.DailyWordClient _wordClient;  // Injected gRPC client for WordServer

        public WordleService(ILogger<WordleService> logger, DailyWord.DailyWordClient wordClient)
        {
            _logger = logger;
            _wordClient = wordClient;  // Store the injected WordClient
        }

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

        public override async Task Play(
            IAsyncStreamReader<PlayRequest> requestStream,
            IServerStreamWriter<PlayReply> responseStream,
            ServerCallContext context)
        {
            string wordOfTheDay = await GetWordOfTheDay();
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
            Console.WriteLine($"[DEBUG] Checking win condition: Word = {wordOfTheDay}");

            while (await requestStream.MoveNext())
            {
                string guess = requestStream.Current.Guess.Trim();

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
}
