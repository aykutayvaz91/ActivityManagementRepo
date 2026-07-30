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

        // İstek/mesaj: gönderilebilecek üst yöneticiler + gönderme.
        Task<System.Collections.Generic.List<MessageRecipientDto>> GetMessageRecipientsAsync();
        Task SendMessageAsync(long recipientEmployeeId, string message);

        // Bildirim tercihleri (geçerli kullanıcı).
        Task<Dto.NotificationPreferenceDto> GetMyPreferencesAsync();
        Task SaveMyPreferencesAsync(Dto.SaveNotificationPreferenceInput input);
    }
}
