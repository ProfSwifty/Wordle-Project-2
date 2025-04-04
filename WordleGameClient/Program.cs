using Grpc.Core;
using Grpc.Net.Client;
using WordleGameServer.Protos;
using System;
using System.Threading.Tasks;
using Google.Protobuf;

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
            Console.WriteLine("Each guess must be a 'playable' 5 letter word.");
            Console.WriteLine("After a guess the game will display a series of\ncharacters to show you how good your guess was.");
            Console.WriteLine("x - means the letter above is not in the word.");
            Console.WriteLine("? - means the letter should be in another spot.");
            Console.WriteLine("* - means the letter is correct in this spot.");
            Console.WriteLine("\tAvailable: a,b,c,d,e,f,g,h,i,j,k,l,m,n,o,p,q,r,s,t,u,v,w,x,y,z");


            using var call = client.Play();

            int attempts = 0;
            string wordOfTheDay = "";

            if (await call.ResponseStream.MoveNext(default))
            {
                var response = call.ResponseStream.Current;
                wordOfTheDay = response.WordOfTheDay;
            }

            try
            {
                while (attempts < 6)
                {
                    Console.Write("\nEnter your guess: ");
                    string? guess = Console.ReadLine()?.Trim();

                    if (string.IsNullOrWhiteSpace(guess) || guess.Length != 5)
                    {
                        Console.WriteLine("Please enter a valid 5-letter word.");
                        continue;
                    }
                    else
                    {
                        attempts++;
                    }

                    await call.RequestStream.WriteAsync(new PlayRequest { Guess = guess });

                    if (await call.ResponseStream.MoveNext(default))
                    {
                        var response = call.ResponseStream.Current;
                        Console.WriteLine(response.Answer);

                        if (guess.Equals(wordOfTheDay, StringComparison.OrdinalIgnoreCase))
                        {
                            Console.WriteLine("\nYou Win!");
                            await DisplayStats(channel);
                            break;
                        }
                    }

                }


                if (attempts >= 6)
                {
                    Console.WriteLine("\nGame Over! The correct word was ", wordOfTheDay);
                    await Task.Delay(1000);
                    await DisplayStats(channel);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }
            finally
            {
                await call.RequestStream.CompleteAsync();
            }
        }

        private static async Task DisplayStats(GrpcChannel channel)
        {
            var client = new DailyWordle.DailyWordleClient(channel);

            // Create a StatsRequest object instead of using Empty
            var statsRequest = new StatsRequest();

            // Call GetStatsAsync with the StatsRequest
            var stats = await client.GetStatsAsync(statsRequest);

            Console.WriteLine("\nGame Statistics:");
            Console.WriteLine($"Total Players: {stats.PlayersCount}");
            Console.WriteLine($"Winners Percent: {stats.WinnersPercent}%");
            Console.WriteLine($"Average Guesses: {stats.AverageGuesses:F2}");
        }
    }
}
