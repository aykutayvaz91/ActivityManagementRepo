using System.Threading.Tasks;
using Abp.Application.Services;
using ActivityManagement.Notifications.Dto;

namespace ActivityManagement.Notifications
{
    public interface INotificationAppService : IApplicationService
    {
        // Zil + açılır liste + polling için özet (okunmamış sayısı + son bildirimler).
        Task<NotificationSummaryDto> GetSummaryAsync();
        Task MarkReadAsync(long id);
        Task MarkAllReadAsync();
    }
}
