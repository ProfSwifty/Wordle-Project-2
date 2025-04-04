using Grpc.Net.Client;
using WordServer.Protos;

namespace WordleServer.Clients
{
    /*
 * Name: Logan McCallum Student Number: 1152955 Section: 2
 * Name: Spencer Martin Student Number: 1040415 Section: 2
 * Name: Ashley Burley-Denis Student Number: 0908968 Section: 1
 */

    //ServerClient class, holds the server information for use with the client
    public class ServerClient
    {
        private static DailyWord.DailyWordClient? _client = null;

        //GetWord method, after connecting to the service, returns the current word of the day.
        public static string GetWord(string word)
        {
            ConnectToService();

            WordReply? reply = _client?.GetWord(new WordRequest { Word = word });

            return reply?.Word ?? "";
        }

        //ConnectToService method, connects the client to the gRPC.
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
