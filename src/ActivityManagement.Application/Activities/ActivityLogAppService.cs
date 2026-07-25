using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Abp.Application.Services;
using Abp.Application.Services.Dto;
using Abp.Domain.Repositories;
using Abp.Linq.Extensions;
using Abp.UI;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ActivityManagement.Activities.Dto;
using ActivityManagement.Entities;

namespace ActivityManagement.Activities
{
    // Efor okuma (kişi kartı/takvim) herkese açık; yazma yalnız yönetici (efor girişinin asıl kapısı
    // ActivitySubject.LogEffortAsync'tir). Yetki manuel claim ile kontrol edilir (projedeki standart).
    public class ActivityLogAppService : ActivityManagementAppServiceBase, IActivityLogAppService
    {
        private readonly IRepository<ActivityLog, long> _activityRepository;
        private readonly IRepository<TaskItem, long> _taskRepository;
        private readonly IRepository<Employee, long> _employeeRepository;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ActivityLogAppService(
            IRepository<ActivityLog, long> activityRepository,
            IRepository<TaskItem, long> taskRepository,
            IRepository<Employee, long> employeeRepository,
            IHttpContextAccessor httpContextAccessor)
        {
            _activityRepository = activityRepository;
            _taskRepository = taskRepository;
            _employeeRepository = employeeRepository;
            _httpContextAccessor = httpContextAccessor;
        }

        private string CurrentRole() => _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Role)?.Value ?? "Uzman";
        private bool IsAdmin() => string.Equals(CurrentRole(), "Admin", StringComparison.OrdinalIgnoreCase);
        // Manager, tüm takımlarda admin gibi kapsam görür (config hariç).
        private bool IsCrossTeamManager() => IsAdmin() || string.Equals(CurrentRole(), "Manager", StringComparison.OrdinalIgnoreCase);
        private bool IsManager()
        {
            var role = CurrentRole();
            return string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(role, "Manager", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(role, "TakımLideri", StringComparison.OrdinalIgnoreCase);
        }

        private long? CurrentEmployeeId()
        {
            var c = _httpContextAccessor.HttpContext?.User?.FindFirst("EmployeeId")?.Value;
            return long.TryParse(c, out var id) ? id : (long?)null;
        }

        // TakımLideri yalnız kendi takımındaki personel için işlem yapabilir; Admin her yerde.
        private async Task EnsureCanActForEmployeeAsync(long targetEmployeeId)
        {
            if (IsCrossTeamManager()) return; // Admin/Manager → tüm takımlar
            var myTeam = await _employeeRepository.GetAll().AsNoTracking()
                .Where(e => e.Id == CurrentEmployeeId()).Select(e => e.TeamId).FirstOrDefaultAsync();
            var targetTeam = await _employeeRepository.GetAll().AsNoTracking()
                .Where(e => e.Id == targetEmployeeId).Select(e => e.TeamId).FirstOrDefaultAsync();
            if (!myTeam.HasValue || targetTeam != myTeam)
                throw new UserFriendlyException("Yalnız kendi takımınızdaki personel için efor işlemi yapabilirsiniz.");
        }

        private async Task RecomputeTaskHoursAsync(long? taskItemId)
        {
            if (!taskItemId.HasValue) return;
            var sum = await _activityRepository.GetAll()
                .Where(l => l.TaskItemId == taskItemId.Value)
                .Select(l => (decimal?)l.HoursSpent).SumAsync() ?? 0m;
            var task = await _taskRepository.FirstOrDefaultAsync(taskItemId.Value);
            if (task != null && task.ActualHours != sum) { task.ActualHours = sum; await CurrentUnitOfWork.SaveChangesAsync(); }
        }

        public async Task<PagedResultDto<ActivityLogDto>> GetAllAsync(GetActivitiesInput input)
        {
            var query = _activityRepository.GetAll()
                .Include(a => a.Employee)
                .Include(a => a.TaskItem)
                .Include(a => a.Project)
                .WhereIf(input.EmployeeId.HasValue, a => a.EmployeeId == input.EmployeeId.Value)
                .WhereIf(input.ProjectId.HasValue, a => a.ProjectId == input.ProjectId.Value)
                .WhereIf(input.StartDate.HasValue, a => a.ActivityDate >= input.StartDate.Value)
                .WhereIf(input.EndDate.HasValue, a => a.ActivityDate <= input.EndDate.Value);

            var count = await query.CountAsync();
            var items = await query.OrderByDescending(a => a.ActivityDate).PageBy(input).ToListAsync();
            return new PagedResultDto<ActivityLogDto>(count, items.Select(MapToDto).ToList());
        }

        public async Task<ActivityLogDto> CreateAsync(CreateActivityLogDto input)
        {
            // Efor girişinin asıl kapısı ActivitySubject.LogEffortAsync'tir; bu doğrudan yol yalnız yöneticiye açık.
            if (!IsManager())
                throw new UserFriendlyException("Efor girişi faaliyet konusu üzerinden yapılır.");
            await EnsureCanActForEmployeeAsync(input.EmployeeId); // TakımLideri yalnız kendi takımı

            var log = new ActivityLog
            {
                TenantId = AbpSession.TenantId ?? 1,
                EmployeeId = input.EmployeeId,
                TaskItemId = input.TaskItemId,
                ProjectId = input.ProjectId,
                Description = input.Description,
                ActivityDate = input.ActivityDate,
                HoursSpent = input.HoursSpent,
                ActivityType = input.ActivityType
            };
            await _activityRepository.InsertAsync(log);
            await CurrentUnitOfWork.SaveChangesAsync();
            await RecomputeTaskHoursAsync(log.TaskItemId);
            return MapToDto(log);
        }

        public async Task DeleteAsync(long id)
        {
            if (!IsManager())
                throw new UserFriendlyException("Efor kaydı silme yetkiniz yok.");
            var log = await _activityRepository.FirstOrDefaultAsync(id);
            if (log == null) return;
            await EnsureCanActForEmployeeAsync(log.EmployeeId); // TakımLideri yalnız kendi takımı
            var taskId = log.TaskItemId;
            await _activityRepository.DeleteAsync(id);
            await CurrentUnitOfWork.SaveChangesAsync();
            await RecomputeTaskHoursAsync(taskId);
        }

        public async Task<List<ActivityLogDto>> GetEmployeeActivitiesAsync(long employeeId, DateTime startDate, DateTime endDate)
        {
            var items = await _activityRepository.GetAll()
                .Include(a => a.TaskItem)
                .Include(a => a.Project)
                .Where(a => a.EmployeeId == employeeId && a.ActivityDate >= startDate && a.ActivityDate <= endDate)
                .OrderByDescending(a => a.ActivityDate)
                .ToListAsync();
            return items.Select(MapToDto).ToList();
        }

        private ActivityLogDto MapToDto(ActivityLog a)
        {
            var dto = ObjectMapper.Map<ActivityLogDto>(a);
            dto.EmployeeName = a.Employee?.FullName;
            dto.TaskTitle = a.TaskItem?.Title;
            dto.ProjectName = a.Project?.Name;
            return dto;
        }
    }
}
