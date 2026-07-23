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
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuditLogAppService(
            IRepository<SystemAuditLog, long> auditRepository,
            IHttpContextAccessor httpContextAccessor)
        {
            _auditRepository = auditRepository;
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
    }
}
