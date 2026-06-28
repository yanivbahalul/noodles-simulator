using System;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using NoodlesSimulator.Models;

namespace NoodlesSimulator.Services;

public static class SupabaseRestClient
{
    public sealed class Context
    {
        public HttpClient? Client { get; init; }
        public string Url { get; init; } = "";
        public bool Enabled { get; init; }
    }

    public static Context Create(IConfiguration config, bool required = false, int timeoutSeconds = 10)
    {
        var url = SupabaseConfiguration.Url(config) ?? "";
        var apiKey = SupabaseConfiguration.ServiceRoleApiKey(config)
                     ?? SupabaseConfiguration.AnonApiKey(config)
                     ?? "";

        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(apiKey))
        {
            if (required)
                throw new InvalidOperationException("Missing Supabase ENV vars.");
            return new Context { Url = url, Enabled = false };
        }

        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(timeoutSeconds) };
        client.DefaultRequestHeaders.Add("apikey", apiKey);
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
        return new Context { Client = client, Url = url, Enabled = true };
    }
}
