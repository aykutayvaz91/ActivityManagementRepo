using System;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Security.Claims;
using System.Threading.Tasks;
using Abp.Domain.Repositories;
using Abp.UI;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ActivityManagement.SystemSettings.Dto;

namespace ActivityManagement.SystemSettings
{
    // Tek satırlık (singleton) e-posta/SMTP ayarları. Sadece Admin görebilir/değiştirebilir/test edebilir.
    public class EmailSettingsAppService : ActivityManagementAppServiceBase, IEmailSettingsAppService
    {
        private readonly IRepository<Entities.EmailSettings, long> _settingsRepository;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public EmailSettingsAppService(
            IRepository<Entities.EmailSettings, long> settingsRepository,
            IHttpContextAccessor httpContextAccessor)
        {
            _settingsRepository = settingsRepository;
            _httpContextAccessor = httpContextAccessor;
        }

        private void EnsureAdmin()
        {
            var role = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Role)?.Value ?? "Uzman";
            if (!string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
                throw new UserFriendlyException("E-posta ayarlarını yalnızca Admin görüntüleyebilir/değiştirebilir.");
        }

        private async Task<Entities.EmailSettings> GetOrCreateSettingsAsync()
        {
            var settings = await _settingsRepository.GetAll().FirstOrDefaultAsync();
            if (settings == null)
            {
                settings = new Entities.EmailSettings { TenantId = AbpSession.TenantId ?? 1 };
                await _settingsRepository.InsertAsync(settings);
                await CurrentUnitOfWork.SaveChangesAsync();
            }
            return settings;
        }

        public async Task<EmailSettingsDto> GetAsync()
        {
            EnsureAdmin();
            var s = await GetOrCreateSettingsAsync();
            return MapToDto(s);
        }

        public async Task<EmailSettingsDto> UpdateAsync(UpdateEmailSettingsDto input)
        {
            EnsureAdmin();
            var s = await GetOrCreateSettingsAsync();
            s.SenderEmail = input.SenderEmail;
            s.SenderDisplayName = input.SenderDisplayName;
            s.SmtpHost = input.SmtpHost;
            s.SmtpPort = input.SmtpPort;
            s.SmtpUserName = input.SmtpUserName;
            if (!string.IsNullOrWhiteSpace(input.SmtpPassword))
                s.SmtpPassword = ActivityManagement.Security.DpapiProtector.Protect(input.SmtpPassword); // DB'de şifreli tut
            s.EnableSsl = input.EnableSsl;
            await CurrentUnitOfWork.SaveChangesAsync();
            return MapToDto(s);
        }

        public async Task SendTestEmailAsync(string toEmail)
        {
            EnsureAdmin();
            var s = await GetOrCreateSettingsAsync();

            if (string.IsNullOrWhiteSpace(s.SmtpHost))
                throw new UserFriendlyException("SMTP ayarları henüz yapılandırılmamış.");

            using var client = new SmtpClient(s.SmtpHost, s.SmtpPort)
            {
                EnableSsl = s.EnableSsl,
                Credentials = string.IsNullOrWhiteSpace(s.SmtpUserName)
                    ? null
                    : new NetworkCredential(s.SmtpUserName, ActivityManagement.Security.DpapiProtector.Unprotect(s.SmtpPassword))
            };

            using var message = new MailMessage
            {
                From = new MailAddress(s.SenderEmail ?? s.SmtpUserName, s.SenderDisplayName),
                Subject = "ActivityManagement - Test E-postası",
                Body = "Bu bir test e-postasıdır. SMTP ayarlarınız doğru şekilde çalışıyor.",
                IsBodyHtml = false
            };
            message.To.Add(toEmail);

            try
            {
                await client.SendMailAsync(message);
            }
            catch (Exception ex)
            {
                throw new UserFriendlyException("Test e-postası gönderilemedi: " + ex.Message);
            }
        }

        private static EmailSettingsDto MapToDto(Entities.EmailSettings s)
        {
            return new EmailSettingsDto
            {
                Id = s.Id,
                SenderEmail = s.SenderEmail,
                SenderDisplayName = s.SenderDisplayName,
                SmtpHost = s.SmtpHost,
                SmtpPort = s.SmtpPort,
                SmtpUserName = s.SmtpUserName,
                HasPassword = !string.IsNullOrEmpty(s.SmtpPassword),
                EnableSsl = s.EnableSsl
            };
        }
    }
}
