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
        private const string WordOfTheDay = "SINGE";

        static async Task Main(string[] args)
        {
            var channel = GrpcChannel.ForAddress("https://localhost:7070");
            var client = new DailyWordle.DailyWordleClient(channel);

            Console.WriteLine("+-------------------+");
            Console.WriteLine("|   W O R D L E D   |");
            Console.WriteLine("+-------------------+");
            Console.WriteLine("\nYou have 6 chances to guess a 5-letter word.");
            Console.WriteLine("\nGuess the 5-letter word:");

            using var call = client.Play();
            int attempts = 0;

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

                    await call.RequestStream.WriteAsync(new PlayRequest { Guess = guess });

                    if (await call.ResponseStream.MoveNext(default))
                    {
                        var response = call.ResponseStream.Current;
                        Console.WriteLine(response.Answer);

                        // Check if the guess is correct locally in the client
                        if (guess.Equals(WordOfTheDay, StringComparison.OrdinalIgnoreCase))
                        {
                            Console.WriteLine("\n🎉 Congratulations! You guessed the correct word! 🎉");
                            UpdateStats(true, attempts + 1);
                            DisplayStats();
                            return; // Exit after a win
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
                Console.WriteLine($"The Word was: {WordOfTheDay}");
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
    }
}
