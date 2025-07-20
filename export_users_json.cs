using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using Newtonsoft.Json;
using Npgsql;

class User
{
    public string Username { get; set; }
    public string Password { get; set; }
    public long? CorrectAnswers { get; set; }
    public long? TotalAnswered { get; set; }
    public bool? IsCheater { get; set; }
    public DateTime? LastSeen { get; set; }
    public bool? IsBanned { get; set; }
}

class Program
{
    static void Main()
    {
        var url = Environment.GetEnvironmentVariable("SUPABASE_URL");
        var key = Environment.GetEnvironmentVariable("SUPABASE_KEY");
        if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(key))
        {
            Console.WriteLine("Missing SUPABASE_URL or SUPABASE_KEY environment variables.");
            return;
        }

        // Supabase connection string (Postgres)
        // Example: "Host=xyz.supabase.co;Port=5432;Username=postgres;Password=YOUR_DB_PASSWORD;Database=postgres;SSL Mode=Require;"
        // You need to get your DB password from Supabase project settings (not the anon/service key)
        var dbPassword = Environment.GetEnvironmentVariable("SUPABASE_DB_PASSWORD");
        if (string.IsNullOrEmpty(dbPassword))
        {
            Console.WriteLine("Missing SUPABASE_DB_PASSWORD environment variable (your database password, not the API key).");
            return;
        }
        var host = new Uri(url).Host;
        var connString = $"Host={host};Port=5432;Username=postgres;Password={dbPassword};Database=postgres;SSL Mode=Require;";

        var users = new List<User>();
        try
        {
            using (var conn = new NpgsqlConnection(connString))
            {
                conn.Open();
                using (var cmd = new NpgsqlCommand("select \"Username\", \"Password\", \"CorrectAnswers\", \"TotalAnswered\", \"IsCheater\", \"LastSeen\", \"IsBanned\" from public.users", conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        users.Add(new User
                        {
                            Username = reader.GetString(0),
                            Password = reader.GetString(1),
                            CorrectAnswers = reader.IsDBNull(2) ? null : (long?)reader.GetInt64(2),
                            TotalAnswered = reader.IsDBNull(3) ? null : (long?)reader.GetInt64(3),
                            IsCheater = reader.IsDBNull(4) ? null : (bool?)reader.GetBoolean(4),
                            LastSeen = reader.IsDBNull(5) ? null : (DateTime?)reader.GetDateTime(5),
                            IsBanned = reader.IsDBNull(6) ? null : (bool?)reader.GetBoolean(6)
                        });
                    }
                }
            }
            var json = JsonConvert.SerializeObject(users, Formatting.Indented);
            File.WriteAllText("users.json", json);
            Console.WriteLine("Exported users to users.json");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
} 