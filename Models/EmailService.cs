using System;
using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace NoodlesSimulator.Models
{
    public class EmailService
    {
        // SMTP fields for Gmail sending
        private readonly string _smtpHost;
        private readonly int _smtpPort;
        private readonly string _smtpUser;
        private readonly string _smtpPass;
        private readonly bool _useSsl;
        private readonly string _emailTo;
        private readonly string _emailFrom;
        private readonly string _sendGridKey;

        public bool IsConfigured { get; }

        public EmailService(IConfiguration configuration)
        {
            Console.WriteLine("[EmailService] Initializing EmailService...");
            
            string Get(string primary, string fallback1 = null, string fallback2 = null)
                => Environment.GetEnvironmentVariable(primary)
                   ?? (fallback1 != null ? Environment.GetEnvironmentVariable(fallback1) : null)
                   ?? (fallback2 != null ? Environment.GetEnvironmentVariable(fallback2) : null)
                   ?? configuration[$"{primary.Replace("__", ":").Replace("Email", "Email")}"]
                   ?? string.Empty;

            _smtpHost = Get("Email__SmtpHost", "EmailSmtpHost");
            var portStr = Get("Email__SmtpPort", "EmailSmtpPort");
            _smtpPort = int.TryParse(portStr, out var p) ? p : 587;
            _smtpUser = Get("Email__SmtpUser");
            var pass = Get("Email__SmtpPass", "EMAIL_SMTP_PASS").Replace(" ", "");
            _smtpPass = string.IsNullOrWhiteSpace(pass) ? configuration["Email:SmtpPass"] ?? string.Empty : pass;
            _useSsl = (Get("Email__UseSsl", "EmailUseSsl").ToLowerInvariant() == "true") || true;
            _emailTo = Get("EMAIL_TO");
            _emailFrom = Get("EMAIL_FROM");

            if (string.IsNullOrWhiteSpace(_emailFrom)) _emailFrom = _smtpUser;
            if (string.IsNullOrWhiteSpace(_smtpHost)) _smtpHost = "smtp.gmail.com";

            // SendGrid API key (preferred for cloud)
            _sendGridKey = Environment.GetEnvironmentVariable("SENDGRID_API_KEY");

            var smtpConfigured = !string.IsNullOrWhiteSpace(_smtpHost)
                           && !string.IsNullOrWhiteSpace(_smtpUser)
                           && !string.IsNullOrWhiteSpace(_smtpPass)
                           && !string.IsNullOrWhiteSpace(_emailTo)
                           && !string.IsNullOrWhiteSpace(_emailFrom);

            var sendGridConfigured = !string.IsNullOrWhiteSpace(_sendGridKey)
                           && !string.IsNullOrWhiteSpace(_emailFrom)
                           && !string.IsNullOrWhiteSpace(_emailTo);

            IsConfigured = sendGridConfigured || smtpConfigured;

            // DEBUG: Print configuration status
            Console.WriteLine($"[EmailService] Configuration loaded:");
            Console.WriteLine($"  - SmtpHost: {(_smtpHost ?? "NULL")}");
            Console.WriteLine($"  - SmtpPort: {_smtpPort}");
            Console.WriteLine($"  - SmtpUser: {(_smtpUser ?? "NULL")}");
            Console.WriteLine($"  - SmtpPass: {(string.IsNullOrWhiteSpace(_smtpPass) ? "NULL/EMPTY" : "***SET***")}");
            Console.WriteLine($"  - UseSsl: {_useSsl}");
            Console.WriteLine($"  - EmailTo: {(_emailTo ?? "NULL")}");
            Console.WriteLine($"  - EmailFrom: {(_emailFrom ?? "NULL")}");
            Console.WriteLine($"  - SendGridKey: {(string.IsNullOrWhiteSpace(_sendGridKey) ? "⚠️ NOT SET - Email will fail on Render!" : "✅ SET (length: " + _sendGridKey.Length + ")")}");
            Console.WriteLine($"  - IsConfigured: {IsConfigured}");
            
            if (!IsConfigured)
            {
                Console.WriteLine("[EmailService] ❌ WARNING: EmailService is NOT properly configured!");
                Console.WriteLine("[EmailService] Missing configuration:");
                if (string.IsNullOrWhiteSpace(_smtpHost)) Console.WriteLine("  - SmtpHost is missing");
                if (string.IsNullOrWhiteSpace(_smtpUser)) Console.WriteLine("  - SmtpUser is missing");
                if (string.IsNullOrWhiteSpace(_smtpPass)) Console.WriteLine("  - SmtpPass is missing");
                if (string.IsNullOrWhiteSpace(_emailTo)) Console.WriteLine("  - EmailTo (EMAIL_TO) is missing");
            }
            else
            {
                Console.WriteLine("[EmailService] ✅ EmailService is properly configured");
            }
        }

        public bool Send(string subject, string htmlBody)
        {
            Console.WriteLine($"[EmailService] Send() called");
            Console.WriteLine($"  - Subject: {subject}");
            Console.WriteLine($"  - Body length: {htmlBody?.Length ?? 0} chars");
            Console.WriteLine($"  - IsConfigured: {IsConfigured}");
            
            if (!IsConfigured)
            {
                Console.WriteLine("[EmailService] ❌ Cannot send - EmailService is NOT configured");
                return false;
            }
            
            try
            {
                // Prefer SendGrid API (works on Render/PaaS that block SMTP)
                if (!string.IsNullOrWhiteSpace(_sendGridKey))
                {
                    Console.WriteLine("[EmailService] ✅ SendGrid API key found, using SendGrid...");
                    var ok = SendViaSendGrid(_emailFrom, _emailTo, subject, htmlBody, _sendGridKey);
                    if (ok)
                    {
                        Console.WriteLine("[EmailService] ✅ Email sent successfully via SendGrid!");
                        return true;
                    }
                    Console.WriteLine("[EmailService] ⚠️ SendGrid failed, trying SMTP fallback...");
                }
                else
                {
                    Console.WriteLine("[EmailService] ⚠️ SendGrid API key NOT found, falling back to SMTP");
                }

                // Check if we're in production (Render blocks SMTP)
                var isProd = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Production";
                if (isProd && string.IsNullOrWhiteSpace(_sendGridKey))
                {
                    Console.WriteLine("[EmailService] ❌ Cannot use SMTP in Production without SendGrid! Please set SENDGRID_API_KEY");
                    return false;
                }

                Console.WriteLine($"[EmailService] Creating mail message (Gmail SMTP)...");
                Console.WriteLine($"  - From: {_emailFrom}");
                Console.WriteLine($"  - To: {_emailTo}");

                using var message = new MailMessage();
                message.From = new MailAddress(_emailFrom);
                message.To.Add(_emailTo);
                message.Subject = subject;
                message.Body = htmlBody;
                message.IsBodyHtml = true;

                Console.WriteLine($"[EmailService] Connecting to SMTP server...");
                Console.WriteLine($"  - Host: {_smtpHost}");
                Console.WriteLine($"  - Port: {_smtpPort}");
                Console.WriteLine($"  - SSL: {_useSsl}");
                Console.WriteLine($"  - User: {_smtpUser}");

                using var client = new SmtpClient(_smtpHost, _smtpPort)
                {
                    EnableSsl = _useSsl,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    UseDefaultCredentials = false,
                    Credentials = new NetworkCredential(_smtpUser, _smtpPass),
                    Timeout = 15000
                };

                Console.WriteLine($"[EmailService] SMTP client configured (Timeout: {client.Timeout}ms). Sending email...");
                client.Send(message);
                Console.WriteLine($"[EmailService] ✅ Email sent successfully via Gmail SMTP!");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EmailService] ❌ Send failed!");
                Console.WriteLine($"  - Exception type: {ex.GetType().Name}");
                Console.WriteLine($"  - Message: {ex.Message}");
                Console.WriteLine($"  - StackTrace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"  - Inner Exception: {ex.InnerException.Message}");
                }
                return false;
            }
        }

        private bool SendViaSendGrid(string fromEmail, string toEmail, string subject, string htmlBody, string apiKey)
        {
            try
            {
                using var http = new HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(15)
                };
                http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var payload = new
                {
                    personalizations = new[] {
                        new {
                            to = new[] { new { email = toEmail } }
                        }
                    },
                    from = new { email = fromEmail },
                    subject = subject,
                    content = new[] {
                        new { type = "text/html", value = htmlBody }
                    }
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var url = "https://api.sendgrid.com/v3/mail/send";

                Console.WriteLine($"[EmailService] POST {url}");
                var resp = http.PostAsync(url, content).GetAwaiter().GetResult();
                var respBody = resp.Content != null ? resp.Content.ReadAsStringAsync().GetAwaiter().GetResult() : string.Empty;
                Console.WriteLine($"[EmailService] SendGrid response: {(int)resp.StatusCode} {resp.ReasonPhrase}");
                
                if ((int)resp.StatusCode == 202)
                {
                    return true;
                }
                
                if (!string.IsNullOrWhiteSpace(respBody))
                {
                    Console.WriteLine($"[EmailService] SendGrid error body: {respBody}");
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EmailService] SendGrid error: {ex.Message}");
                return false;
            }
        }
    }
}
