using Grpc.Core;
using Grpc.Net.Client;
using WordleGameServer.Protos;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace WordleClient
{
    internal class Program
    {
        private const string StatsFile = "wordle_stats.json";

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
            Console.WriteLine("After a guess the game will display a series of");
            Console.WriteLine("characters to show you how good your guess was.");
            Console.WriteLine("x - means the letter above is not in the word.");
            Console.WriteLine("? - means the letter should be in another spot.");
            Console.WriteLine("* - means the letter is correct in this spot.");
            Console.WriteLine();
            Console.WriteLine("     Available: a,b,c,d,e,f,g,h,i,j,k,l,m,n,o,p,q,r,s,t,u,v,w,x,y,z");

            using var call = client.Play(); // No arguments passed here

            int attempts = 0;

            // Get Word of the Day from the server
            string wordOfTheDay = "";

            // Wait for the first response that contains the WordOfTheDay
            if (await call.ResponseStream.MoveNext(default))
            {
                var response = call.ResponseStream.Current;
                wordOfTheDay = response.WordOfTheDay; // Get the Word of the Day once
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

                    // Write the guess to the request stream
                    await call.RequestStream.WriteAsync(new PlayRequest { Guess = guess });

                    // Read the response from the server
                    if (await call.ResponseStream.MoveNext(default))
                    {
                        var response = call.ResponseStream.Current;
                        Console.WriteLine(response.Answer);

                        //Check if the guess is correct locally in the client
                        if (guess.Equals(wordOfTheDay, StringComparison.OrdinalIgnoreCase))
                        {
                            Console.WriteLine("\nYou Win!");
                            UpdateStats(true, attempts + 1);
                            DisplayStats();
                            return;
                        }
                    }
                    else
                    {
                        Console.WriteLine("Server closed the connection unexpectedly.");
                        break;
                    }

                    attempts++;
                }

                Console.WriteLine("\nGame Over! The correct word was not guessed.");
                Console.WriteLine($"The Word was: {wordOfTheDay}");
                UpdateStats(false, attempts);
                DisplayStats();
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
            {
                Console.WriteLine("The game was cancelled unexpectedly. Please restart the application.");
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

        private static void UpdateStats(bool isWinner, int attempts)
        {
            var stats = LoadStats();
            DateTime currentDate = DateTime.Today;

            // Reset stats if it's a new day
            if (stats.LastUpdated != currentDate)
            {
                stats.Players = 0;
                stats.Winners = 0;
                stats.TotalGuesses = 0;
                stats.LastUpdated = currentDate;
            }

            stats.Players++;
            if (isWinner) stats.Winners++;
            stats.TotalGuesses += attempts;
            File.WriteAllText(StatsFile, JsonSerializer.Serialize(stats, new JsonSerializerOptions { WriteIndented = true }));
        }

        private static WordleStats LoadStats()
        {
            if (File.Exists(StatsFile))
            {
                return JsonSerializer.Deserialize<WordleStats>(File.ReadAllText(StatsFile)) ?? new WordleStats();
            }
            return new WordleStats();
        }

        private static void DisplayStats()
        {
            var stats = LoadStats();
            Console.WriteLine("\nGame Statistics:");
            Console.WriteLine($"Total Players: {stats.Players}");
            Console.WriteLine($"Winners: {stats.Winners}");
            Console.WriteLine($"Total Guesses: {stats.TotalGuesses}");
            Console.WriteLine($"Average Guesses per Player: {stats.AverageGuesses:F2}");
        }
    }

    public class WordleStats
    {
        public int Players { get; set; } = 0;
        public int Winners { get; set; } = 0;
        public int TotalGuesses { get; set; } = 0;
        public double AverageGuesses => Players > 0 ? (double)TotalGuesses / Players : 0;
        public DateTime LastUpdated { get; set; } = DateTime.MinValue;
    }
}
