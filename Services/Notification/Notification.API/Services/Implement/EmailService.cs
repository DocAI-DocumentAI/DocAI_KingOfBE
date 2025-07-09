using Microsoft.Extensions.Options;
using Notification.API.Services.Interfaces;
using System.Net.Mail;
using System.Net;

namespace Notification.API.Services.Implement
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<bool> SendEmailAsync(string recipient, string subject, string body)
        {
            try
            {
                var smtpHost = _configuration["AppSettings:SmtpHost"];
                var smtpPort = int.Parse(_configuration["AppSettings:SmtpPort"]);
                var smtpUsername = _configuration["AppSettings:SmtpUsername"];
                var smtpPassword = _configuration["AppSettings:SmtpPassword"];

                using var client = new SmtpClient(smtpHost, smtpPort)
                {
                    Credentials = new NetworkCredential(smtpUsername, smtpPassword),
                    EnableSsl = true
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(smtpUsername),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                };
                mailMessage.To.Add(recipient);

                await client.SendMailAsync(mailMessage);
                _logger.LogInformation("Email sent successfully to {Recipient}", recipient);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {Recipient}", recipient);
                return false;
            }
        }
    }
}
