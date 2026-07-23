using Abp.Domain.Entities;
using Abp.Domain.Entities.Auditing;

namespace ActivityManagement.Entities
{
    // Tek satırlık (singleton) SMTP/e-posta yapılandırması. Admin panelinden yönetilir.
    public class EmailSettings : Entity<long>, IMustHaveTenant
    {
        public int TenantId { get; set; }

        public string SenderEmail { get; set; }
        public string SenderDisplayName { get; set; }
        public string SmtpHost { get; set; }
        public int SmtpPort { get; set; } = 587;
        public string SmtpUserName { get; set; }
        public string SmtpPassword { get; set; }
        public bool EnableSsl { get; set; } = true;
    }
}
