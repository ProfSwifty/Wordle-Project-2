using Grpc.Net.Client;
using WordServer.Protos;

namespace WordleServer.Clients
{
    public class ServerClient
    {
        private static DailyWord.DailyWordClient? _client = null;

        public static string GetWord(string word)
        {
            ConnectToService();

            WordReply? reply = _client?.GetWord(new WordRequest { Word = word });

            return reply?.Word ?? "";
        }

        private static void ConnectToService()
        {
            if (_client is null)
            {
                var channel = GrpcChannel.ForAddress("https://localhost:7206");
                _client = new DailyWord.DailyWordClient(channel);
            }
        }
    }

}
