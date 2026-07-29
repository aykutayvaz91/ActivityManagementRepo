using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Abp.Application.Services.Dto;
using Abp.Domain.Repositories;
using Abp.Linq.Extensions;
using Abp.UI;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ActivityManagement.Auditing.Dto;
using ActivityManagement.Entities;

namespace ActivityManagement.Auditing
{
    // Sistem denetim logları — yalnızca Admin görüntüleyebilir.
    public class AuditLogAppService : ActivityManagementAppServiceBase, IAuditLogAppService
    {
        private readonly IRepository<SystemAuditLog, long> _auditRepository;
        private readonly IRepository<SystemAuditLogArchive, long> _archiveRepository;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuditLogAppService(
            IRepository<SystemAuditLog, long> auditRepository,
            IRepository<SystemAuditLogArchive, long> archiveRepository,
            IHttpContextAccessor httpContextAccessor)
        {
            _auditRepository = auditRepository;
            _archiveRepository = archiveRepository;
            _httpContextAccessor = httpContextAccessor;
        }

        private void EnsureAdmin()
        {
            var role = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Role)?.Value ?? "Uzman";
            if (!string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
                throw new UserFriendlyException("Sistem loglarını yalnızca Admin görüntüleyebilir.");
        }

        public async Task<PagedResultDto<AuditLogDto>> GetAllAsync(GetAuditLogsInput input)
        {
            EnsureAdmin();
            var query = _auditRepository.GetAll().AsNoTracking()
                .WhereIf(input.StartDate.HasValue, a => a.ExecutionTime >= input.StartDate.Value.Date)
                .WhereIf(input.EndDate.HasValue, a => a.ExecutionTime < input.EndDate.Value.Date.AddDays(1))
                .WhereIf(!string.IsNullOrWhiteSpace(input.UserName), a => a.UserName.Contains(input.UserName))
                .WhereIf(!string.IsNullOrWhiteSpace(input.ActionType), a => a.ActionType == input.ActionType)
                .WhereIf(!string.IsNullOrWhiteSpace(input.EntityName), a => a.EntityName == input.EntityName);

            var count = await query.CountAsync();
            var items = await query
                .OrderByDescending(a => a.ExecutionTime)
                .PageBy(input)
                .ToListAsync();

            return new PagedResultDto<AuditLogDto>(count, items.Select(a => ObjectMapper.Map<AuditLogDto>(a)).ToList());
        }

        // ARŞİV sorgu — yıllık arşive taşınmış (geçmiş yıllara ait) denetim kayıtları. Yalnız Admin.
        public async Task<PagedResultDto<AuditLogDto>> GetArchiveAsync(GetAuditLogsInput input)
        {
            EnsureAdmin();
            var query = _archiveRepository.GetAll().AsNoTracking()
                .WhereIf(input.StartDate.HasValue, a => a.ExecutionTime >= input.StartDate.Value.Date)
                .WhereIf(input.EndDate.HasValue, a => a.ExecutionTime < input.EndDate.Value.Date.AddDays(1))
                .WhereIf(!string.IsNullOrWhiteSpace(input.UserName), a => a.UserName.Contains(input.UserName))
                .WhereIf(!string.IsNullOrWhiteSpace(input.ActionType), a => a.ActionType == input.ActionType)
                .WhereIf(!string.IsNullOrWhiteSpace(input.EntityName), a => a.EntityName == input.EntityName);

            var count = await query.CountAsync();
            var items = await query
                .OrderByDescending(a => a.ExecutionTime)
                .PageBy(input)
                .ToListAsync();

            return new PagedResultDto<AuditLogDto>(count, items.Select(a => ObjectMapper.Map<AuditLogDto>(a)).ToList());
        }

        // Arşiv özeti (kayıt sayısı + en eski/yeni tarih) — ekran başlığında gösterilir.
        public async Task<ArchiveSummaryDto> GetArchiveSummaryAsync()
        {
            EnsureAdmin();
            var q = _archiveRepository.GetAll().AsNoTracking();
            var count = await q.CountAsync();
            if (count == 0) return new ArchiveSummaryDto { Count = 0 };
            return new ArchiveSummaryDto
            {
                Count = count,
                Oldest = await q.MinAsync(a => (DateTime?)a.ExecutionTime),
                Newest = await q.MaxAsync(a => (DateTime?)a.ExecutionTime)
            };
        }
    }
}
