using Grpc.Core;
using Grpc.Net.Client;
using WordleGameServer.Protos;
using System;
using System.Threading.Tasks;

namespace WordleClient
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var channel = GrpcChannel.ForAddress("https://localhost:7070");
            var client = new DailyWordle.DailyWordleClient(channel);

            Console.WriteLine("+-------------------+");
            Console.WriteLine("|   W O R D L E D   |");
            Console.WriteLine("+-------------------+");
            Console.WriteLine();
            Console.WriteLine("You have 6 chances to guess a 5-letter word.");
            Console.WriteLine("Each guess must be a 'playable' 5-letter word.");
            Console.WriteLine("After a guess, the game will display feedback:");
            Console.WriteLine("x - Letter is not in the word.");
            Console.WriteLine("? - Letter is in the word but in the wrong spot.");
            Console.WriteLine("* - Letter is correct and in the right spot.");
            Console.WriteLine("\nGuess the 5-letter word:");

            using var call = client.Play();

            int attempts = 0;
            while (attempts < 6)
            {
                Console.Write("\nEnter your guess: ");
                string guess = Console.ReadLine()?.Trim().ToUpper() ?? "";

                if (guess.Length != 5)
                {
                    Console.WriteLine("Please enter a 5-letter word.");
                    continue;
                }

                await call.RequestStream.WriteAsync(new PlayRequest { Guess = guess });
                var response = await call.ResponseStream.MoveNext(default);

                if (!response) break;

                Console.WriteLine(call.ResponseStream.Current.Answer);

                if (call.ResponseStream.Current.Answer.Contains("You win!") ||
                    call.ResponseStream.Current.Answer.Contains("Game Over"))
                {
                    break;
                }

                attempts++;
            }

            Console.WriteLine("\nGame Over!");
            await call.RequestStream.CompleteAsync();
        }
    }
}
