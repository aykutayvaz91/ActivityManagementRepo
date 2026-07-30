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

        // İstek/mesaj: gönderilebilecek üst yöneticiler
        [HttpGet]
        public async Task<IActionResult> Recipients()
        {
            try { return Json(await _svc.GetMessageRecipientsAsync()); }
            catch { return Json(new System.Collections.Generic.List<object>()); }
        }

        // Bildirim tercihleri ekranı (geçerli kullanıcı)
        [HttpGet]
        public async Task<IActionResult> Preferences()
        {
            var dto = await _svc.GetMyPreferencesAsync();
            return View(dto);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Preferences(bool emailEnabled, System.Collections.Generic.List<int> enabledTypes)
        {
            try
            {
                await _svc.SaveMyPreferencesAsync(new SaveNotificationPreferenceInput
                {
                    EmailEnabled = emailEnabled,
                    EnabledInAppTypes = enabledTypes ?? new System.Collections.Generic.List<int>()
                });
                TempData["Success"] = "Bildirim tercihleri kaydedildi.";
            }
            catch (Abp.UI.UserFriendlyException ex) { TempData["Uyari"] = ex.Message; }
            catch (System.Exception ex) { ActivityManagement.Logging.ErrorLog.Write(ex, "Notifications/Preferences"); TempData["Uyari"] = "Tercihler kaydedilemedi."; }
            return RedirectToAction("Preferences");
        }

        // İstek/mesaj gönder (üst yöneticiye)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendMessage(long recipientEmployeeId, string message)
        {
            try { await _svc.SendMessageAsync(recipientEmployeeId, message); return Ok(new { ok = true }); }
            catch (Abp.UI.UserFriendlyException ex) { return new ContentResult { StatusCode = 400, Content = ex.Message, ContentType = "text/plain; charset=utf-8" }; }
            catch (System.Exception ex) { ActivityManagement.Logging.ErrorLog.Write(ex, "Notifications/SendMessage"); return new ContentResult { StatusCode = 500, Content = "İstek gönderilemedi.", ContentType = "text/plain; charset=utf-8" }; }
        }
    }
}
