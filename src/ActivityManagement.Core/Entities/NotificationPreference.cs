using Abp.Domain.Entities;

namespace ActivityManagement.Entities
{
    // Kişiye özel bildirim tercihi (kullanıcı kendi ayarlar). Kayıt yoksa VARSAYILAN: her şey açık.
    //   EmailEnabled: e-posta bildirimleri (SLA, görev atama vb.) bu kişiye gönderilsin mi.
    //   MutedTypes:  in-app'te SUSTURULAN NotificationType değerleri (virgülle ayrık int listesi).
    public class NotificationPreference : Entity<long>, IMustHaveTenant
    {
        public int TenantId { get; set; }
        public long EmployeeId { get; set; }
        public bool EmailEnabled { get; set; } = true;
        public string MutedTypes { get; set; }
    }
}
