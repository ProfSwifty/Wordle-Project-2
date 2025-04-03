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
        private const string WordFile = "daily_word.json";

        public WordleService(ILogger<WordleService> logger)
        {
            _logger = logger;
        }

        private string GetWordOfTheDay()
        {
            if (File.Exists(WordFile))
            {
                var wordData = JsonConvert.DeserializeObject<DailyWordEntry>(File.ReadAllText(WordFile));
                return wordData?.Word ?? "ERROR";
            }
            return "ERROR";
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

            while (await requestStream.MoveNext())  // Keep accepting guesses
            {
                string guess = requestStream.Current.Guess.Trim();

                if (playerGuesses[playerId].Contains(guess))
                {
                    await responseStream.WriteAsync(new PlayReply { Answer = "You already guessed that word!"});
                    continue;
                }

                playerGuesses[playerId].Add(guess);


                string feedback = GenerateFeedback(wordOfTheDay, guess);
                await responseStream.WriteAsync(new PlayReply { Answer = feedback });

            }

        }


        private string GenerateFeedback(string wordOfTheDay, string guess)
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
                }
                else if (matches[letter] < letterCountInWord)
                {
                    feedback[i] = '?';
                    matches[letter]++;
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
