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
        private readonly string _emailFromName;
        private readonly string _brevoApiKey;

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
            _emailFromName = Get("EMAIL_FROM_NAME");

            if (string.IsNullOrWhiteSpace(_emailFrom)) _emailFrom = _smtpUser;
            if (string.IsNullOrWhiteSpace(_smtpHost)) _smtpHost = "smtp.gmail.com";

            // Brevo (Sendinblue) API key (preferred for cloud)
            _brevoApiKey = Environment.GetEnvironmentVariable("BREVO_API_KEY");

            var smtpConfigured = !string.IsNullOrWhiteSpace(_smtpHost)
                           && !string.IsNullOrWhiteSpace(_smtpUser)
                           && !string.IsNullOrWhiteSpace(_smtpPass)
                           && !string.IsNullOrWhiteSpace(_emailTo)
                           && !string.IsNullOrWhiteSpace(_emailFrom);

            var brevoConfigured = !string.IsNullOrWhiteSpace(_brevoApiKey)
                           && !string.IsNullOrWhiteSpace(_emailFrom)
                           && !string.IsNullOrWhiteSpace(_emailTo);

            IsConfigured = brevoConfigured || smtpConfigured;

            // DEBUG: Print configuration status
            Console.WriteLine($"[EmailService] Configuration loaded:");
            Console.WriteLine($"  - SmtpHost: {(_smtpHost ?? "NULL")}");
            Console.WriteLine($"  - SmtpPort: {_smtpPort}");
            Console.WriteLine($"  - SmtpUser: {(_smtpUser ?? "NULL")}");
            Console.WriteLine($"  - SmtpPass: {(string.IsNullOrWhiteSpace(_smtpPass) ? "NULL/EMPTY" : "***SET***")}");
            Console.WriteLine($"  - UseSsl: {_useSsl}");
            Console.WriteLine($"  - EmailTo: {(_emailTo ?? "NULL")}");
            Console.WriteLine($"  - EmailFrom: {(_emailFrom ?? "NULL")}");
            Console.WriteLine($"  - EmailFromName: {(_emailFromName ?? "NULL")}");
            Console.WriteLine($"  - BrevoApiKey: {(string.IsNullOrWhiteSpace(_brevoApiKey) ? "NOT SET - Email will fail on Render!" : "SET (length: " + _brevoApiKey.Length + ")")}");
            Console.WriteLine($"  - IsConfigured: {IsConfigured}");
            
            if (!IsConfigured)
            {
                Console.WriteLine("[EmailService] WARNING: EmailService is NOT properly configured!");
                Console.WriteLine("[EmailService] Missing configuration:");
                if (string.IsNullOrWhiteSpace(_smtpHost)) Console.WriteLine("  - SmtpHost is missing");
                if (string.IsNullOrWhiteSpace(_smtpUser)) Console.WriteLine("  - SmtpUser is missing");
                if (string.IsNullOrWhiteSpace(_smtpPass)) Console.WriteLine("  - SmtpPass is missing");
                if (string.IsNullOrWhiteSpace(_emailTo)) Console.WriteLine("  - EmailTo (EMAIL_TO) is missing");
            }
            else
            {
                Console.WriteLine("[EmailService] EmailService is properly configured");
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
                Console.WriteLine("[EmailService] Cannot send - EmailService is NOT configured");
                return false;
            }
            
            try
            {
                // Prefer Brevo API (works on Render/PaaS that block SMTP)
                if (!string.IsNullOrWhiteSpace(_brevoApiKey))
                {
                    Console.WriteLine("[EmailService] Brevo API key found, using Brevo...");
                    var ok = SendViaBrevo(_emailFrom, _emailTo, subject, htmlBody, _brevoApiKey);
                    if (ok)
                    {
                        Console.WriteLine("[EmailService] Email sent successfully via Brevo!");
                        return true;
                    }
                    Console.WriteLine("[EmailService] Brevo failed, trying SMTP fallback...");
                }
                else
                {
                    Console.WriteLine("[EmailService] Brevo API key NOT found, falling back to SMTP");
                }

                // Check if we're in production (Render blocks SMTP)
                var isProd = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Production";
                if (isProd && string.IsNullOrWhiteSpace(_brevoApiKey))
                {
                    Console.WriteLine("[EmailService] Cannot use SMTP in Production without Brevo! Please set BREVO_API_KEY");
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
                Console.WriteLine($"[EmailService] Email sent successfully via Gmail SMTP!");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EmailService] Send failed!");
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

        private bool SendViaBrevo(string fromEmail, string toEmail, string subject, string htmlBody, string apiKey)
        {
            try
            {
                using var http = new HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(15)
                };
                // Brevo uses "api-key" header instead of Bearer auth
                http.DefaultRequestHeaders.Add("api-key", apiKey);
                http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                // Brevo v3 SMTP API payload
                var payload = new
                {
                    sender = new { email = fromEmail, name = string.IsNullOrWhiteSpace(_emailFromName) ? "NoodlesSimulator" : _emailFromName },
                    to = new[] { new { email = toEmail } },
                    subject = subject,
                    htmlContent = htmlBody
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var url = "https://api.brevo.com/v3/smtp/email";

                Console.WriteLine($"[EmailService] POST {url}");
                var resp = http.PostAsync(url, content).GetAwaiter().GetResult();
                var respBody = resp.Content != null ? resp.Content.ReadAsStringAsync().GetAwaiter().GetResult() : string.Empty;
                Console.WriteLine($"[EmailService] Brevo response: {(int)resp.StatusCode} {resp.ReasonPhrase}");

                // Brevo returns 201 on success for /v3/smtp/email
                if ((int)resp.StatusCode == 201)
                {
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(respBody))
                {
                    Console.WriteLine($"[EmailService] Brevo error body: {respBody}");
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EmailService] Brevo error: {ex.Message}");
                return false;
            }
        }
    }
}
