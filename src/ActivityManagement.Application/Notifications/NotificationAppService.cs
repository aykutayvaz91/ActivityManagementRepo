using System;
using System.Linq;
using System.Threading.Tasks;
using Abp.Domain.Repositories;
using Abp.Linq.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ActivityManagement.Entities;
using ActivityManagement.Notifications.Dto;

namespace ActivityManagement.Notifications
{
    // Bildirim OKUMA/İŞARETLEME (in-app). Her zaman geçerli kullanıcının EmployeeId'sine göre çalışır
    // (login-as ile o kişinin bildirimleri görünür). Başkasının bildirimine erişilemez.
    public class NotificationAppService : ActivityManagementAppServiceBase, INotificationAppService
    {
        private readonly IRepository<Notification, long> _repo;
        private readonly IRepository<Employee, long> _empRepo;
        private readonly IHttpContextAccessor _http;

        public NotificationAppService(IRepository<Notification, long> repo, IRepository<Employee, long> empRepo, IHttpContextAccessor http)
        {
            _repo = repo;
            _empRepo = empRepo;
            _http = http;
        }

        private long? CurrentEmployeeId()
        {
            var v = _http.HttpContext?.User?.FindFirst("EmployeeId")?.Value;
            return long.TryParse(v, out var id) ? id : (long?)null;
        }

        private string CurrentRole() => _http.HttpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "Uzman";

        // Rol rütbesi: Uzman(1) < TakımLideri(2) < Manager(3) < Admin(4).
        private static int RoleRank(string role) =>
            string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) ? 4 :
            string.Equals(role, "Manager", StringComparison.OrdinalIgnoreCase) ? 3 :
            string.Equals(role, "TakımLideri", StringComparison.OrdinalIgnoreCase) ? 2 : 1;

        private static string RoleLabel(string role) =>
            string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) ? "Yönetim (Admin)" :
            string.Equals(role, "Manager", StringComparison.OrdinalIgnoreCase) ? "Manager" :
            string.Equals(role, "TakımLideri", StringComparison.OrdinalIgnoreCase) ? "Takım Lideri" : "Uzman";

        public async Task<NotificationSummaryDto> GetSummaryAsync()
        {
            var dto = new NotificationSummaryDto();
            var empId = CurrentEmployeeId();
            if (!empId.HasValue) return dto;   // personel kaydı yoksa (Sistem Yöneticisi) boş

            dto.UnreadCount = await _repo.GetAll().AsNoTracking()
                .CountAsync(n => n.RecipientEmployeeId == empId.Value && !n.IsRead);

            var recent = await _repo.GetAll().AsNoTracking()
                .Where(n => n.RecipientEmployeeId == empId.Value)
                .OrderByDescending(n => n.Id)
                .Take(15)
                .ToListAsync();

            dto.Recent = recent.Select(n => new NotificationDto
            {
                Id = n.Id,
                Type = n.Type,
                Title = n.Title,
                Message = n.Message,
                Link = n.Link,
                Icon = n.Icon,
                Severity = n.Severity,
                IsRead = n.IsRead,
                CreationTime = n.CreationTime,
                TimeAgo = TimeAgo(n.CreationTime)
            }).ToList();
            return dto;
        }

        public async Task MarkReadAsync(long id)
        {
            var empId = CurrentEmployeeId();
            if (!empId.HasValue) return;
            var n = await _repo.FirstOrDefaultAsync(x => x.Id == id && x.RecipientEmployeeId == empId.Value);
            if (n != null && !n.IsRead) { n.IsRead = true; await CurrentUnitOfWork.SaveChangesAsync(); }
        }

        public async Task MarkAllReadAsync()
        {
            var empId = CurrentEmployeeId();
            if (!empId.HasValue) return;
            var list = await _repo.GetAll().Where(x => x.RecipientEmployeeId == empId.Value && !x.IsRead).ToListAsync();
            foreach (var n in list) n.IsRead = true;
            if (list.Count > 0) await CurrentUnitOfWork.SaveChangesAsync();
        }

        // İstek/mesaj gönderilebilecek ÜST yöneticiler (senden yüksek rütbe). Admin (Sistem Yön.) dahil → ona da ulaşılır.
        public async Task<System.Collections.Generic.List<MessageRecipientDto>> GetMessageRecipientsAsync()
        {
            var myId = CurrentEmployeeId();
            int myRank = RoleRank(CurrentRole());
            var list = await _empRepo.GetAll().AsNoTracking()
                .Where(e => e.IsActive && (!myId.HasValue || e.Id != myId.Value))
                .Select(e => new { e.Id, e.FirstName, e.LastName, e.AppRole, e.IsSystemAccount })
                .ToListAsync();
            return list
                .Where(e => RoleRank(e.AppRole) > myRank) // yalnız üst rütbe
                .Select(e => new MessageRecipientDto
                {
                    Id = e.Id,
                    Name = (e.FirstName + " " + e.LastName).Trim() + (e.IsSystemAccount ? " (Yönetim)" : ""),
                    RoleLabel = RoleLabel(e.AppRole)
                })
                .OrderBy(x => x.RoleLabel).ThenBy(x => x.Name)
                .ToList();
        }

        // İstek/mesaj gönder: yalnız ÜST rütbeye (üst yöneticiye). Alıcının zil bildirimine düşer.
        public async Task SendMessageAsync(long recipientEmployeeId, string message)
        {
            var myId = CurrentEmployeeId();
            if (!myId.HasValue)
                throw new Abp.UI.UserFriendlyException("İstek göndermek için personel kaydınız bulunmuyor.");
            if (string.IsNullOrWhiteSpace(message))
                throw new Abp.UI.UserFriendlyException("Mesaj boş olamaz.");
            if (message.Length > 1000) message = message.Substring(0, 1000);

            var me = await _empRepo.GetAll().AsNoTracking()
                .Where(e => e.Id == myId.Value).Select(e => new { e.FirstName, e.LastName, e.AppRole }).FirstOrDefaultAsync();
            var to = await _empRepo.GetAll().AsNoTracking()
                .Where(e => e.Id == recipientEmployeeId).Select(e => new { e.AppRole, e.IsActive }).FirstOrDefaultAsync();
            if (to == null || !to.IsActive)
                throw new Abp.UI.UserFriendlyException("Alıcı bulunamadı.");
            if (RoleRank(to.AppRole) <= RoleRank(me?.AppRole))
                throw new Abp.UI.UserFriendlyException("Yalnızca üst yöneticinize istek/mesaj gönderebilirsiniz.");

            var senderName = (me == null) ? "Bir personel" : (me.FirstName + " " + me.LastName).Trim();
            await _repo.InsertAsync(new Notification
            {
                TenantId = AbpSession.TenantId ?? 1,
                RecipientEmployeeId = recipientEmployeeId,
                Type = NotificationType.Mesaj,
                Title = $"{senderName} — İstek/Mesaj",
                Message = message.Trim(),
                Link = $"/Employees/Card/{myId.Value}",   // gönderenin kartı
                Icon = "fa-paper-plane",
                Severity = "info",
                IsRead = false
            });
            await CurrentUnitOfWork.SaveChangesAsync();
        }

        private static string TimeAgo(DateTime t)
        {
            var span = DateTime.Now - t;
            if (span.TotalMinutes < 1) return "az önce";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} dk önce";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours} saat önce";
            if (span.TotalDays < 7) return $"{(int)span.TotalDays} gün önce";
            return t.ToString("dd.MM.yyyy");
        }
    }
}
