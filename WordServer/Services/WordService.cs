using Grpc.Core;
using Newtonsoft.Json;
using WordServer.Protos;
using System.IO;

namespace WordServer.Services
{
    public class WordService : DailyWord.DailyWordBase
    {
        private readonly List<string> _wordList;
        private readonly string _dailyWord;
        private static string DailyWordFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "daily_word.json");


        public WordService()
        {
            _wordList = LoadData();
            _dailyWord = GetOrGenerateDailyWord();
        }

        public override Task<WordReply> GetWord(WordRequest request, ServerCallContext context)
        {
            return Task.FromResult(new WordReply { Word = _dailyWord });
        }

        public override Task<IsValid> ValidateWord(WordInput request, ServerCallContext context)
        {
            bool isWordValid = _wordList.Contains(request.Word.ToLower());
            return Task.FromResult(new IsValid { IsValid_ = isWordValid });
        }

        private List<string> LoadData()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wordle.json");

            if (!File.Exists(path))
            {
                Console.WriteLine("wordle.json not found!");
                throw new FileNotFoundException("wordle.json not found!");
            }

            Console.WriteLine("Loaded wordle.json");
            var words = JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(path));
            return words ?? new List<string>();
        }

        private string GetOrGenerateDailyWord()
        {
            Console.WriteLine($"Checking for {DailyWordFile}... Exists? {File.Exists(DailyWordFile)}");

            // Ensure the directory exists where the file is supposed to be saved
            string directory = Path.GetDirectoryName(DailyWordFile);
            if (!Directory.Exists(directory))
            {
                Console.WriteLine($"Directory does not exist, creating directory: {directory}");
                Directory.CreateDirectory(directory); // Create directory if it doesn't exist
            }

            if (File.Exists(DailyWordFile))
            {
                try
                {
                    var data = JsonConvert.DeserializeObject<DailyWordData>(File.ReadAllText(DailyWordFile));
                    Console.WriteLine($"Found daily_word.json: {JsonConvert.SerializeObject(data)}");
                    if (data?.Date == DateTime.UtcNow.Date.ToString("yyyy-MM-dd"))
                    {
                        Console.WriteLine("Using existing daily word");
                        return data.Word;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error reading daily_word.json: {ex.Message}");
                }
            }

            string newWord = SelectDailyWord();
            var newEntry = new DailyWordData
            {
                Word = newWord,
                Date = DateTime.UtcNow.Date.ToString("yyyy-MM-dd")
            };

            var json = JsonConvert.SerializeObject(newEntry, Formatting.Indented);
            Console.WriteLine($"Writing new daily word: {json}");

            try
            {
                File.WriteAllText(DailyWordFile, json);
                Console.WriteLine("daily_word.json created successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to write daily_word.json: {ex.Message}");
            }

            return newWord;
        }

        private string SelectDailyWord()
        {
            if (_wordList.Count == 0)
            {
                Console.WriteLine("Word list is empty! Returning ERROR");
                return "ERROR";
            }

            string selectedWord = _wordList[new Random().Next(_wordList.Count)];
            Console.WriteLine($"Selected new word: {selectedWord}");
            return selectedWord;
        }

        private class DailyWordData
        {
            public string Word { get; set; }
            public string Date { get; set; }
        }
        public string SelectRandomWord()
        {
            if (_wordList.Count == 0)
            {
                Console.WriteLine("Word list is empty! Returning error.");
                return "ERROR";
            }

            string selectedWord = _wordList[new Random().Next(_wordList.Count)];
            Console.WriteLine($"Selected word: {selectedWord}");
            return selectedWord;
        }
    }
}
