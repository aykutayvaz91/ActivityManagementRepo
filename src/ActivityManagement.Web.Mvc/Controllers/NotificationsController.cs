using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ActivityManagement.Notifications;
using ActivityManagement.Notifications.Dto;

namespace ActivityManagement.Web.Controllers
{
    // In-app bildirim: polling özeti (GET) + okundu işaretleme (POST). EmployeeId'ye göre AppService'te kısıtlı.
    public class NotificationsController : ActivityManagementControllerBase
    {
        private readonly INotificationAppService _svc;

        public NotificationsController(INotificationAppService svc)
        {
            _svc = svc;
        }

        [HttpGet]
        public async Task<IActionResult> Summary()
        {
            try { return Json(await _svc.GetSummaryAsync()); }
            catch { return Json(new NotificationSummaryDto()); }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkRead(long id)
        {
            try { await _svc.MarkReadAsync(id); } catch { }
            return Ok();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllRead()
        {
            try { await _svc.MarkAllReadAsync(); } catch { }
            return Ok();
        }
    }
}
