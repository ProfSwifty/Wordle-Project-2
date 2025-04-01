using Grpc.Core;
using Newtonsoft.Json;
using WordServer.Protos;
using System.IO;

public class WordService : DailyWord.DailyWordBase
{
    private readonly List<string> _wordList;
    private readonly string _dailyWord;
    private const string DailyWordFile = "daily_word.json";

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
        string path = "wordle.json";
        if (!File.Exists(path)) throw new FileNotFoundException("wordle.json not found!");

        var words = JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(path));
        return words ?? new List<string>();
    }

    private string GetOrGenerateDailyWord()
    {
        if (File.Exists(DailyWordFile))
        {
            try
            {
                var data = JsonConvert.DeserializeObject<DailyWordData>(File.ReadAllText(DailyWordFile));
                if (data?.Date == DateTime.UtcNow.Date.ToString("yyyy-MM-dd"))
                {
                    return data.Word; 
                }
            }
            catch
            {
                
            }
        }

        string newWord = SelectDailyWord();
        File.WriteAllText(DailyWordFile, JsonConvert.SerializeObject(new DailyWordData
        {
            Word = newWord,
            Date = DateTime.UtcNow.Date.ToString("yyyy-MM-dd")
        }));

        return newWord;
    }

    private string SelectDailyWord()
    {
        return _wordList.Count > 0 ? _wordList[new Random().Next(_wordList.Count)] : "ERROR";
    }

    private class DailyWordData
    {
        public string Word { get; set; }
        public string Date { get; set; }
    }
}
