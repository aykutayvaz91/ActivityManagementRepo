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
        private readonly IHttpContextAccessor _http;

        public NotificationAppService(IRepository<Notification, long> repo, IHttpContextAccessor http)
        {
            _repo = repo;
            _http = http;
        }

        private long? CurrentEmployeeId()
        {
            var v = _http.HttpContext?.User?.FindFirst("EmployeeId")?.Value;
            return long.TryParse(v, out var id) ? id : (long?)null;
        }

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
