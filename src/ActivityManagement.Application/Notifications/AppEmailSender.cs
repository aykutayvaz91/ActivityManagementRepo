using System;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Abp.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using ActivityManagement.Entities;

namespace ActivityManagement.Notifications
{
    public class AppEmailSender : IAppEmailSender
    {
        private readonly IRepository<EmailSettings, long> _settingsRepository;

        public AppEmailSender(IRepository<EmailSettings, long> settingsRepository)
        {
            _settingsRepository = settingsRepository;
        }

        public async Task<bool> SendAsync(string toEmail, string subject, string htmlBody)
        {
            if (string.IsNullOrWhiteSpace(toEmail)) return false;
            try
            {
                var s = await _settingsRepository.GetAll().AsNoTracking().FirstOrDefaultAsync();
                // SMTP yapılandırılmamışsa sessizce çık (bildirim altyapısı pasif)
                if (s == null || string.IsNullOrWhiteSpace(s.SmtpHost)) return false;

                using var client = new SmtpClient(s.SmtpHost, s.SmtpPort)
                {
                    EnableSsl = s.EnableSsl,
                    Credentials = string.IsNullOrWhiteSpace(s.SmtpUserName)
                        ? null
                        : new NetworkCredential(s.SmtpUserName, s.SmtpPassword)
                };
                using var message = new MailMessage
                {
                    From = new MailAddress(s.SenderEmail ?? s.SmtpUserName, s.SenderDisplayName ?? "Faaliyet Yönetimi"),
                    Subject = subject,
                    Body = htmlBody,
                    IsBodyHtml = true
                };
                message.To.Add(toEmail);
                await client.SendMailAsync(message);
                return true;
            }
            catch (Exception ex)
            {
                // Bildirim hatası ana akışı (görev oluşturma vb.) bozmamalı — sadece logla
                ActivityManagement.Logging.ErrorLog.Write(ex, $"AppEmailSender→{toEmail}");
                return false;
            }
        }
    }
}
