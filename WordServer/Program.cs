using Microsoft.AspNetCore.Cors.Infrastructure;
using WordServer.Services;

namespace Wordle


/*
* Name: Logan McCallum Student Number: 1152955 Section: 2
* Name: Spencer Martin Student Number: 1040415 Section: 2
* Name: Ashley Burley-Denis Student Number: 0908968 Section: 1
*/
{
    //Program Class, sets and runs the GRPC for handling requests.
    public class Program
    {
        public static void Main(string[] args)
        {


            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddGrpc();


            var app = builder.Build();

            app.MapGrpcService<WordService>();
            app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");
            app.Run();
        }
    }
}