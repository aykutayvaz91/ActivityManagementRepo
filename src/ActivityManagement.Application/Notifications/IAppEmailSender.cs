using System.Threading.Tasks;
using Abp.Dependency;

namespace ActivityManagement.Notifications
{
    // Uygulama içi bildirim e-postası gönderimi. SMTP yapılandırılmamışsa sessizce no-op döner
    // (altyapı hazır; SMTP bilgileri Admin panelden girilince otomatik çalışır).
    public interface IAppEmailSender : ITransientDependency
    {
        // true: gönderildi, false: SMTP yok/gönderilemedi (hata fırlatmaz — çağıran akışı bozmaz)
        Task<bool> SendAsync(string toEmail, string subject, string htmlBody);
    }
}
