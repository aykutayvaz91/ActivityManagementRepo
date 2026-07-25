using System.Threading.Tasks;
using Abp.Dependency;
using ActivityManagement.Entities;

namespace ActivityManagement.Notifications
{
    // Bildirim OLUŞTURMA arabirimi. IApplicationService DEĞİL → dış API'ye açılmaz (spam engeli).
    // Diğer AppService'ler / hosted service'ler enjekte edip çağırır.
    public interface INotificationManager : ITransientDependency
    {
        // recipientEmployeeId'ye bildirim oluşturur. actorEmployeeId verilirse ve alıcıyla aynıysa atlanır
        // (kendi yaptığın işlem için kendine bildirim gitmez). Alıcı yoksa (null/0) hiçbir şey yapmaz.
        Task NotifyAsync(long? recipientEmployeeId, NotificationType type, string title, string message,
                         string link = null, string icon = null, string severity = "info", long? actorEmployeeId = null);
    }
}
