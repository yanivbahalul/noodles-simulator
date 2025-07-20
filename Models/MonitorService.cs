using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using MailKit.Net.Smtp;
using MimeKit;

namespace NoodlesSimulator.Models
{
    public class MonitorService : BackgroundService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _config;
        private readonly string _checkUrl;
        private readonly int _intervalSeconds;
        private readonly string _renderApiKey;
        private readonly string _serviceId;
        private readonly string _emailFrom;
        private readonly string _emailTo;
        private readonly string _emailSmtpUser;
        private readonly string _emailSmtpPass;
        private readonly string _emailSmtpServer;
        private readonly string _emailSubject;

        public MonitorService(IHttpClientFactory httpClientFactory, IConfiguration config)
        {
            _httpClientFactory = httpClientFactory;
            _config = config;
            _checkUrl = _config["CHECK_URL"] ?? "https://noodles-simulator.onrender.com";
            _intervalSeconds = int.TryParse(_config["CHECK_INTERVAL"], out var s) ? s : 60;
            _renderApiKey = _config["RENDER_API_KEY"];
            _serviceId = _config["SERVICE_ID"];
            _emailFrom = _config["EMAIL_FROM"] ?? "yanivbahlul@gmail.com";
            _emailTo = _config["EMAIL_TO"] ?? "yanivbahlul@gmail.com";
            _emailSmtpUser = _config["EMAIL_SMTP_USER"] ?? "yanivbahlul@gmail.com";
            _emailSmtpPass = _config["EMAIL_SMTP_PASS"];
            _emailSmtpServer = _config["EMAIL_SMTP_SERVER"] ?? "smtp.gmail.com";
            _emailSubject = _config["EMAIL_SUBJECT"] ?? "[Noodles Simulator] Restarted!";
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Console.WriteLine("[MonitorService] Started background monitoring");
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var client = _httpClientFactory.CreateClient();
                    var resp = await client.GetAsync(_checkUrl, stoppingToken);
                    if ((int)resp.StatusCode >= 500)
                    {
                        Console.WriteLine($"[MonitorService] Detected error {(int)resp.StatusCode}, restarting service...");
                        await RestartRenderService();
                        await SendEmailNotification($"HTTP {(int)resp.StatusCode}");
                    }
                    else
                    {
                        Console.WriteLine($"[MonitorService] Site is up: {(int)resp.StatusCode}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[MonitorService] Site unreachable: {ex.Message}, restarting service...");
                    await RestartRenderService();
                    await SendEmailNotification($"Exception: {ex.Message}");
                }
                await Task.Delay(TimeSpan.FromSeconds(_intervalSeconds), stoppingToken);
            }
        }

        private async Task RestartRenderService()
        {
            if (string.IsNullOrEmpty(_renderApiKey) || string.IsNullOrEmpty(_serviceId))
            {
                Console.WriteLine("[MonitorService] RENDER_API_KEY or SERVICE_ID missing, cannot restart.");
                return;
            }
            try
            {
                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _renderApiKey);
                var url = $"https://api.render.com/v1/services/{_serviceId}/deploys";
                var resp = await client.PostAsync(url, new StringContent("{}", Encoding.UTF8, "application/json"));
                if (resp.IsSuccessStatusCode)
                    Console.WriteLine("[MonitorService] Restarted Render service successfully!");
                else
                    Console.WriteLine($"[MonitorService] Failed to restart service: {(int)resp.StatusCode} {await resp.Content.ReadAsStringAsync()}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MonitorService] Restart error: {ex.Message}");
            }
        }

        private async Task SendEmailNotification(string reason)
        {
            if (string.IsNullOrEmpty(_emailSmtpPass))
            {
                Console.WriteLine("[MonitorService] EMAIL_SMTP_PASS missing, cannot send email.");
                return;
            }
            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress("Noodles Simulator", _emailFrom));
                message.To.Add(new MailboxAddress("Yaniv Bahlul", _emailTo));
                message.Subject = _emailSubject;
                message.Body = new TextPart("plain")
                {
                    Text = $"The server at {_checkUrl} was restarted due to: {reason}"
                };
                using var client = new SmtpClient();
                await client.ConnectAsync(_emailSmtpServer, 587, MailKit.Security.SecureSocketOptions.StartTls);
                await client.AuthenticateAsync(_emailSmtpUser, _emailSmtpPass);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);
                Console.WriteLine("[MonitorService] Email notification sent.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MonitorService] Failed to send email: {ex.Message}");
            }
        }
    }
} 