using System.Net;
using System.Net.Mail;

namespace Auth.API.Utils;

public class EmailUtil
{
    public static bool SendEmail(string toEmail, string subject, string body, IConfiguration configuration)
    {
        try
        {
            var smtpServer = configuration["Email:SmtpServer"];
            var smtpPort = int.Parse(configuration["Email:Port"]);
            var smtpUser = configuration["Email:Username"];
            var smtpPass = configuration["Email:Password"];
            var displayName = configuration["Email:DisplayName"];

            using var smtpClient = new SmtpClient(smtpServer)
            {
                Port = smtpPort,
                Credentials = new NetworkCredential(smtpUser, smtpPass),
                EnableSsl = true,
                UseDefaultCredentials = false
            };
            
            using var mailMessage = new MailMessage
            {
                From = new MailAddress(smtpUser, displayName),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };

            mailMessage.To.Add(toEmail);
            smtpClient.Send(mailMessage);

            Console.WriteLine($" Email đã được gửi thành công đến {toEmail}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($" Lỗi gửi email: {ex.Message}");
            return false;
        }
    }
}