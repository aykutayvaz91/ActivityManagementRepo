using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abp.Application.Services;
using Abp.Application.Services.Dto;
using Abp.AutoMapper;
using Abp.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using ActivityManagement.Entities;

namespace ActivityManagement.Workflow
{
    [AutoMapFrom(typeof(WorkflowStatus))]
    public class WorkflowStatusDto : EntityDto<int>
    {
        public string Name { get; set; }
        public string Color { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; }
        public bool IsCompletedState { get; set; }
        public int StatusValue { get; set; }
    }

    public class CreateUpdateWorkflowStatusDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Color { get; set; } = "secondary";
        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsCompletedState { get; set; }
        public int StatusValue { get; set; }
    }

    public interface IWorkflowStatusAppService : IApplicationService
    {
        Task<List<WorkflowStatusDto>> GetAllAsync(bool onlyActive = false);
        Task<WorkflowStatusDto> CreateAsync(CreateUpdateWorkflowStatusDto input);
        Task<WorkflowStatusDto> UpdateAsync(CreateUpdateWorkflowStatusDto input);
        Task DeleteAsync(int id);
    }

    public class WorkflowStatusAppService : ActivityManagementAppServiceBase, IWorkflowStatusAppService
    {
        private readonly IRepository<WorkflowStatus, int> _repo;
        private readonly Microsoft.AspNetCore.Http.IHttpContextAccessor _http;

        public WorkflowStatusAppService(IRepository<WorkflowStatus, int> repo, Microsoft.AspNetCore.Http.IHttpContextAccessor http)
        {
            _repo = repo;
            _http = http;
        }

        // Durum tanımları görev semantiğini belirler → yalnız Admin yönetir (API'den doğrudan çağrıya karşı).
        private void EnsureAdmin()
        {
            var role = _http.HttpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "Uzman";
            if (!string.Equals(role, "Admin", System.StringComparison.OrdinalIgnoreCase))
                throw new Abp.UI.UserFriendlyException("İş akışı durumlarını yalnızca Admin yönetebilir.");
        }

        public async Task<List<WorkflowStatusDto>> GetAllAsync(bool onlyActive = false)
        {
            var q = _repo.GetAll();
            if (onlyActive) q = q.Where(s => s.IsActive);
            var list = await q.OrderBy(s => s.SortOrder).ToListAsync();
            return ObjectMapper.Map<List<WorkflowStatusDto>>(list);
        }

        public async Task<WorkflowStatusDto> CreateAsync(CreateUpdateWorkflowStatusDto input)
        {
            EnsureAdmin();
            var e = new WorkflowStatus
            {
                Name = input.Name, Color = input.Color, SortOrder = input.SortOrder,
                IsActive = input.IsActive, IsCompletedState = input.IsCompletedState, StatusValue = input.StatusValue
            };
            await _repo.InsertAsync(e);
            await CurrentUnitOfWork.SaveChangesAsync();
            return ObjectMapper.Map<WorkflowStatusDto>(e);
        }

        public async Task<WorkflowStatusDto> UpdateAsync(CreateUpdateWorkflowStatusDto input)
        {
            EnsureAdmin();
            var e = await _repo.GetAsync(input.Id);
            e.Name = input.Name; e.Color = input.Color; e.SortOrder = input.SortOrder;
            e.IsActive = input.IsActive; e.IsCompletedState = input.IsCompletedState; e.StatusValue = input.StatusValue;
            return ObjectMapper.Map<WorkflowStatusDto>(e);
        }

        public async Task DeleteAsync(int id)
        {
            EnsureAdmin();
            await _repo.DeleteAsync(id);
        }
    }
}
