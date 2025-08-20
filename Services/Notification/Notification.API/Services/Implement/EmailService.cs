using Microsoft.Extensions.Options;
using Notification.API.Services.Interfaces;
using System.Net.Mail;
using System.Net;
using Microsoft.Extensions.Caching.Memory;

namespace Notification.API.Services.Implement
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;
        private readonly IMemoryCache _cache;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger, IMemoryCache cache)
        {
            _configuration = configuration;
            _logger = logger;
            _cache = cache;
        }

        public async Task<bool> SendEmailAsync(string recipient, string subject, string body)
        {
            if (!CanSendEmailAsync())
            {
                return false;
            }

            try
            {
                var smtpHost = _configuration["Email:SmtpHost"] ?? "smtp.gmail.com";
                var smtpPort = _configuration.GetValue<int>("Email:SmtpPort", 587);
                var fromEmail = _configuration["Email:FromEmail"] ?? "phucnhse173025@fpt.edu.vn";
                var fromPassword = _configuration["Email:FromPassword"] ?? "anuk yrxy ksyj ecga";

                using var client = new SmtpClient(smtpHost, smtpPort)
                {
                    Credentials = new NetworkCredential(fromEmail, fromPassword),
                    EnableSsl = true
                };

                using var message = new MailMessage(fromEmail, recipient, subject, body)
                {
                    IsBodyHtml = true
                };

                await client.SendMailAsync(message);
                await RecordEmailSentAsync();

                _logger.LogInformation("Email sent successfully to {Recipient}", recipient);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {Recipient}", recipient);
                return false;
            }
        }

        public async Task<int> GetRemainingEmailsAsync()
        {
            var hourlyCount = GetCurrentCount("email_hourly");
            var dailyCount = GetCurrentCount("email_daily");
            var maxHourly = _configuration.GetValue<int>("Email:MaxPerHour", 100);
            var maxDaily = _configuration.GetValue<int>("Email:MaxPerDay", 1000);

            var hourlyRemaining = Math.Max(0, maxHourly - hourlyCount);
            var dailyRemaining = Math.Max(0, maxDaily - dailyCount);

            return Math.Min(hourlyRemaining, dailyRemaining);
        }

        private bool CanSendEmailAsync()
        {
            var maxHourly = _configuration.GetValue<int>("Email:MaxPerHour", 100);
            var maxDaily = _configuration.GetValue<int>("Email:MaxPerDay", 1000);

            var hourlyCount = GetCurrentCount("email_hourly");
            var dailyCount = GetCurrentCount("email_daily");

            return hourlyCount < maxHourly && dailyCount < maxDaily;
        }

        private async Task RecordEmailSentAsync()
        {
            IncrementCount("email_hourly", TimeSpan.FromHours(1));
            IncrementCount("email_daily", TimeSpan.FromDays(1));
        }

        private int GetCurrentCount(string key)
        {
            return _cache.TryGetValue(key, out int count) ? count : 0;
        }

        private void IncrementCount(string key, TimeSpan expiry)
        {
            var currentCount = GetCurrentCount(key);
            _cache.Set(key, currentCount + 1, expiry);
        }
    }
}

