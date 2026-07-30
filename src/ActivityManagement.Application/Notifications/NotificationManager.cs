using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abp.Domain.Repositories;
using Abp.Domain.Uow;
using Microsoft.EntityFrameworkCore;
using ActivityManagement.Entities;

namespace ActivityManagement.Notifications
{
    public class NotificationManager : INotificationManager
    {
        private readonly IRepository<Notification, long> _notificationRepository;
        private readonly IRepository<NotificationPreference, long> _prefRepository;
        // İstek/loop başına tercih cache'i (SLA gibi çok çağrılan akışlarda tekrar sorgu önlenir).
        private readonly Dictionary<long, NotificationPreference> _prefCache = new();

        public NotificationManager(
            IRepository<Notification, long> notificationRepository,
            IRepository<NotificationPreference, long> prefRepository)
        {
            _notificationRepository = notificationRepository;
            _prefRepository = prefRepository;
        }

        private async Task<NotificationPreference> GetPrefAsync(long employeeId)
        {
            if (_prefCache.TryGetValue(employeeId, out var cached)) return cached;
            var p = await _prefRepository.GetAll().AsNoTracking().FirstOrDefaultAsync(x => x.EmployeeId == employeeId);
            _prefCache[employeeId] = p; // null da cache'lenir (kayıt yok = varsayılan açık)
            return p;
        }

        private static bool IsMuted(NotificationPreference pref, NotificationType type)
        {
            if (pref == null || string.IsNullOrWhiteSpace(pref.MutedTypes)) return false;
            return pref.MutedTypes.Split(',', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries)
                .Any(x => int.TryParse(x, out var v) && v == (int)type);
        }

        // E-posta bildirimleri bu kişiye açık mı (SLA/atama e-postaları önce buna bakar). Kayıt yoksa varsayılan AÇIK.
        public async Task<bool> IsEmailEnabledAsync(long employeeId)
        {
            var p = await GetPrefAsync(employeeId);
            return p == null || p.EmailEnabled;
        }

        [UnitOfWork]
        public virtual async Task NotifyAsync(long? recipientEmployeeId, NotificationType type, string title, string message,
            string link = null, string icon = null, string severity = "info", long? actorEmployeeId = null)
        {
            if (!recipientEmployeeId.HasValue || recipientEmployeeId.Value <= 0) return;
            // Kendi işlemi için kendine bildirim gönderme
            if (actorEmployeeId.HasValue && actorEmployeeId.Value == recipientEmployeeId.Value) return;

            // Kişi bu tipi in-app'te susturmuşsa bildirim oluşturma.
            var pref = await GetPrefAsync(recipientEmployeeId.Value);
            if (IsMuted(pref, type)) return;

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
