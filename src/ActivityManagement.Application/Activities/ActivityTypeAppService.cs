using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Abp.Domain.Repositories;
using Abp.UI;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ActivityManagement.Activities.Dto;
using ActivityManagement.Entities;

namespace ActivityManagement.Activities
{
    // Dinamik Faaliyet Tipi yönetimi (Admin). Yetki manuel claim ile kontrol edilir.
    public class ActivityTypeAppService : ActivityManagementAppServiceBase, IActivityTypeAppService
    {
        private readonly IRepository<ActivityTypeDef, int> _repo;
        private readonly IHttpContextAccessor _http;

        public ActivityTypeAppService(IRepository<ActivityTypeDef, int> repo, IHttpContextAccessor http)
        {
            _repo = repo;
            _http = http;
        }

        private bool IsAdmin()
        {
            var role = _http.HttpContext?.User?.FindFirst(ClaimTypes.Role)?.Value ?? "Uzman";
            return string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase);
        }

        private void EnsureAdmin()
        {
            if (!IsAdmin())
                throw new UserFriendlyException("Faaliyet tipi yönetimi yalnızca Admin tarafından yapılabilir.");
        }

        public async Task<List<ActivityTypeDefDto>> GetAllAsync(bool onlyActive = false)
        {
            var q = _repo.GetAll().AsNoTracking();
            if (onlyActive) q = q.Where(t => t.IsActive);
            var items = await q.OrderBy(t => t.SortOrder).ThenBy(t => t.Name).ToListAsync();
            return items.Select(Map).ToList();
        }

        public async Task<ActivityTypeDefDto> CreateAsync(CreateUpdateActivityTypeDto input)
        {
            EnsureAdmin();
            if (string.IsNullOrWhiteSpace(input.Name))
                throw new UserFriendlyException("Faaliyet tipi adı zorunludur.");
            var entity = new ActivityTypeDef
            {
                TenantId = AbpSession.TenantId ?? 1,
                Name = input.Name.Trim(),
                SortOrder = input.SortOrder,
                IsActive = input.IsActive
            };
            await _repo.InsertAsync(entity);
            await CurrentUnitOfWork.SaveChangesAsync();
            return Map(entity);
        }

        public async Task<ActivityTypeDefDto> UpdateAsync(CreateUpdateActivityTypeDto input)
        {
            EnsureAdmin();
            var entity = await _repo.GetAsync(input.Id);
            if (!string.IsNullOrWhiteSpace(input.Name)) entity.Name = input.Name.Trim();
            entity.SortOrder = input.SortOrder;
            entity.IsActive = input.IsActive;
            await CurrentUnitOfWork.SaveChangesAsync();
            return Map(entity);
        }

        public async Task DeleteAsync(int id)
        {
            EnsureAdmin();
            await _repo.DeleteAsync(id);
        }

        private static ActivityTypeDefDto Map(ActivityTypeDef t) => new ActivityTypeDefDto
        {
            Id = t.Id, Name = t.Name, SortOrder = t.SortOrder, IsActive = t.IsActive
        };
    }
}
