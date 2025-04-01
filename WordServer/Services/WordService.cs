using Grpc.Core;
using Newtonsoft.Json;
using WordServer.Protos;
using System.IO;

public class WordService : DailyWord.DailyWordBase
{
    private readonly List<string> _wordList;
    private readonly string _dailyWord;

    public WordService()
    {
        _wordList = LoadData();
        _dailyWord = SelectDailyWord();
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

    private string SelectDailyWord()
    {
        return _wordList.Count > 0 ? _wordList[new Random().Next(_wordList.Count)] : "ERROR";
    }
}
