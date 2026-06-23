using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abp.Application.Services.Dto;
using Abp.Authorization;
using Abp.Domain.Repositories;
using Abp.Linq.Extensions;
using Microsoft.EntityFrameworkCore;
using ActivityManagement.Authorization;
using ActivityManagement.Employees.Dto;
using ActivityManagement.Entities;

namespace ActivityManagement.Employees
{
    [AbpAuthorize(ActivityManagementPermissions.Employees.Default)]
    public class EmployeeAppService : ActivityManagementAppServiceBase, IEmployeeAppService
    {
        private readonly IRepository<Employee, long> _employeeRepository;
        private readonly IRepository<Responsibility, long> _responsibilityRepository;
        private readonly IRepository<ProjectEmployee, long> _projectEmployeeRepository;
        private readonly IRepository<TaskItem, long> _taskRepository;

        public EmployeeAppService(
            IRepository<Employee, long> employeeRepository,
            IRepository<Responsibility, long> responsibilityRepository,
            IRepository<ProjectEmployee, long> projectEmployeeRepository,
            IRepository<TaskItem, long> taskRepository)
        {
            _employeeRepository = employeeRepository;
            _responsibilityRepository = responsibilityRepository;
            _projectEmployeeRepository = projectEmployeeRepository;
            _taskRepository = taskRepository;
        }

        public async Task<PagedResultDto<EmployeeDto>> GetAllAsync(GetEmployeesInput input)
        {
            var query = _employeeRepository.GetAll()
                .WhereIf(!string.IsNullOrWhiteSpace(input.Filter),
                    e => e.FirstName.Contains(input.Filter) || e.LastName.Contains(input.Filter) ||
                         e.Department.Contains(input.Filter) || e.Title.Contains(input.Filter))
                .WhereIf(!string.IsNullOrWhiteSpace(input.Department),
                    e => e.Department == input.Department)
                .WhereIf(input.IsActive.HasValue, e => e.IsActive == input.IsActive.Value);

            var count = await query.CountAsync();
            var items = await query
                .OrderBy(e => e.LastName)
                .PageBy(input)
                .ToListAsync();

            return new PagedResultDto<EmployeeDto>(count, ObjectMapper.Map<List<EmployeeDto>>(items));
        }

        public async Task<EmployeeDto> GetAsync(long id)
        {
            var employee = await _employeeRepository.GetAsync(id);
            return ObjectMapper.Map<EmployeeDto>(employee);
        }

        public async Task<EmployeeDto> GetCardAsync(long id)
        {
            var employee = await _employeeRepository.GetAll()
                .Include(e => e.Responsibilities)
                .Include(e => e.ProjectEmployees).ThenInclude(pe => pe.Project)
                .FirstOrDefaultAsync(e => e.Id == id);

            var dto = ObjectMapper.Map<EmployeeDto>(employee);

            dto.Responsibilities = employee.Responsibilities
                .Where(r => r.IsActive)
                .OrderBy(r => r.OrderNo)
                .Select(r => new ResponsibilityDto { Id = r.Id, Title = r.Title, Description = r.Description, OrderNo = r.OrderNo })
                .ToList();

            dto.AssignedProjects = employee.ProjectEmployees
                .Select(pe => new AssignedProjectDto
                {
                    Id = pe.ProjectId,
                    Name = pe.Project.Name,
                    Code = pe.Project.Code,
                    Role = pe.Role,
                    Status = pe.Project.Status.ToString()
                }).ToList();

            var tasks = await _taskRepository.GetAll()
                .Where(t => t.AssignedEmployeeId == id)
                .ToListAsync();

            dto.PendingTaskCount = tasks.Count(t => t.Status == Entities.TaskStatus.Beklemede);
            dto.InProgressTaskCount = tasks.Count(t => t.Status == Entities.TaskStatus.DevamEdiyor);
            dto.CompletedTaskCount = tasks.Count(t => t.Status == Entities.TaskStatus.Tamamlandi);

            return dto;
        }

        [AbpAuthorize(ActivityManagementPermissions.Employees.Create)]
        public async Task<EmployeeDto> CreateAsync(CreateUpdateEmployeeDto input)
        {
            var employee = ObjectMapper.Map<Employee>(input);
            employee.TenantId = AbpSession.TenantId ?? 1;
            await _employeeRepository.InsertAsync(employee);
            await CurrentUnitOfWork.SaveChangesAsync();
            return ObjectMapper.Map<EmployeeDto>(employee);
        }

        [AbpAuthorize(ActivityManagementPermissions.Employees.Edit)]
        public async Task<EmployeeDto> UpdateAsync(CreateUpdateEmployeeDto input)
        {
            var employee = await _employeeRepository.GetAsync(input.Id);
            ObjectMapper.Map(input, employee);
            await _employeeRepository.UpdateAsync(employee);
            return ObjectMapper.Map<EmployeeDto>(employee);
        }

        [AbpAuthorize(ActivityManagementPermissions.Employees.Delete)]
        public async Task DeleteAsync(long id)
        {
            await _employeeRepository.DeleteAsync(id);
        }

        public async Task<ListResultDto<EmployeeDto>> GetAllListAsync()
        {
            var employees = await _employeeRepository.GetAll()
                .Where(e => e.IsActive)
                .OrderBy(e => e.LastName)
                .ToListAsync();
            return new ListResultDto<EmployeeDto>(ObjectMapper.Map<List<EmployeeDto>>(employees));
        }
    }
}
