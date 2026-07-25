using System.Threading.Tasks;
using Abp.Domain.Repositories;
using Abp.Domain.Uow;
using ActivityManagement.Entities;

namespace ActivityManagement.Notifications
{
    public class NotificationManager : INotificationManager
    {
        private readonly IRepository<Notification, long> _notificationRepository;

        public NotificationManager(IRepository<Notification, long> notificationRepository)
        {
            _notificationRepository = notificationRepository;
        }

        [UnitOfWork]
        public virtual async Task NotifyAsync(long? recipientEmployeeId, NotificationType type, string title, string message,
            string link = null, string icon = null, string severity = "info", long? actorEmployeeId = null)
        {
            if (!recipientEmployeeId.HasValue || recipientEmployeeId.Value <= 0) return;
            // Kendi işlemi için kendine bildirim gönderme
            if (actorEmployeeId.HasValue && actorEmployeeId.Value == recipientEmployeeId.Value) return;

            await _notificationRepository.InsertAsync(new Notification
            {
                TenantId = 1,
                RecipientEmployeeId = recipientEmployeeId.Value,
                Type = type,
                Title = title,
                Message = message,
                Link = link,
                Icon = string.IsNullOrWhiteSpace(icon) ? DefaultIcon(type) : icon,
                Severity = string.IsNullOrWhiteSpace(severity) ? "info" : severity,
                IsRead = false
            });
        }

        private static string DefaultIcon(NotificationType type) => type switch
        {
            NotificationType.GorevAtandi => "fa-clipboard-list",
            NotificationType.TalepAtandi => "fa-inbox",
            NotificationType.SlaYaklasti => "fa-triangle-exclamation",
            NotificationType.DurumDegisti => "fa-flag",
            NotificationType.YorumEklendi => "fa-comment",
            NotificationType.FaaliyetAtandi => "fa-clipboard-check",
            _ => "fa-bell"
        };
    }
}
