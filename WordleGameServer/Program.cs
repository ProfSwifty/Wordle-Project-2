using WordServer.Protos;
using WordleServer.Services;

namespace WordleServer
{
 /*
 * Name: Logan McCallum Student Number: 1152955 Section: 2
 * Name: Spencer Martin Student Number: 1040415 Section: 2
 * Name: Ashley Burley-Denis Student Number: 0908968 Section: 1
 */

    //Program class, sets up the gRPC client to get the DailyWordClient 
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddGrpc();

            builder.Services.AddGrpcClient<DailyWord.DailyWordClient>(o =>
            {
                o.Address = new Uri("https://localhost:7206");
            });


            var app = builder.Build();

            app.MapGrpcService<WordleService>();

            app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client.");

            app.Run();
        }
    }
}