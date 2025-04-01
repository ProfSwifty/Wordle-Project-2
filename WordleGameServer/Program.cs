using WordServer.Protos;
using WordleServer.Services;

namespace WordleServer
{
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
            app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

            app.Run();
        }
    }
}