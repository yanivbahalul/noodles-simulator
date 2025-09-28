using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace NoodlesSimulator.Models
{
    public class EmailService
    {
        private readonly ILogger<EmailService> _logger;
        private readonly IConfiguration _configuration;
        private readonly SemaphoreSlim _emailThrottler;
        private readonly Dictionary<string, DateTime> _lastEmailSent = new();
        private readonly int _maxEmailsPerHour;
        private readonly int _maxRetriesOnFailure;

        public EmailService(ILogger<EmailService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
            _emailThrottler = new SemaphoreSlim(5);
            _maxEmailsPerHour = configuration.GetValue<int>("Email:MaxPerHour", 100);
            _maxRetriesOnFailure = configuration.GetValue<int>("Email:MaxRetries", 3);
        }

        public async Task<bool> SendEmailAsync(string to, string subject, string htmlBody)
        {
            try
            {
                // Rate limiting per recipient
                if (_lastEmailSent.ContainsKey(to))
                {
                    var timeSinceLastEmail = DateTime.UtcNow.Subtract(_lastEmailSent[to]);
                    if (timeSinceLastEmail.TotalSeconds < 60) // min 1 minute between emails
                    {
                        _logger.LogWarning($"Rate limit exceeded for recipient: {to}");
                        return false;
                    }
                }

                // Get email configuration
                var smtpServer = _configuration["Email:SmtpServer"] ?? "smtp.gmail.com";
                var smtpPort = int.Parse(_configuration["Email:SmtpPort"] ?? "587");
                var smtpUser = _configuration["Email:SmtpUser"];
                var smtpPass = _configuration["Email:SmtpPass"];

                if (string.IsNullOrEmpty(smtpUser) || string.IsNullOrEmpty(smtpPass))
                {
                    _logger.LogError("Missing SMTP credentials");
                    return false;
                }

                // Throttle concurrent email sending
                await _emailThrottler.WaitAsync();

                try
                {
                    var message = new MimeMessage();
                    message.From.Add(new MailboxAddress("Noodles Simulator", smtpUser));
                    message.To.Add(new MailboxAddress("", to));
                    message.Subject = subject;

                    var bodyBuilder = new BodyBuilder { HtmlBody = htmlBody };
                    message.Body = bodyBuilder.ToMessageBody();

                    int retryCount = 0;
                    while (retryCount < _maxRetriesOnFailure)
                    {
                        try
                        {
                            using var client = new SmtpClient();
                            await client.ConnectAsync(smtpServer, smtpPort, SecureSocketOptions.StartTls);
                            await client.AuthenticateAsync(smtpUser, smtpPass);
                            await client.SendAsync(message);
                            await client.DisconnectAsync(true);

                            _lastEmailSent[to] = DateTime.UtcNow;
                            return true;
                        }
                        catch (Exception ex) when (retryCount < _maxRetriesOnFailure - 1)
                        {
                            _logger.LogWarning($"Email send attempt {retryCount + 1} failed: {ex.Message}");
                            retryCount++;
                            await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, retryCount))); // exponential backoff
                        }
                    }

                    return false;
                }
                finally
                {
                    _emailThrottler.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Email service error: {ex}");
                return false;
            }
        }
    }
} 