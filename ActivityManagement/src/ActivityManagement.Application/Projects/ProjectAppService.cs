using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abp.Application.Services;
using Abp.Application.Services.Dto;
using Abp.Authorization;
using Abp.Domain.Repositories;
using Abp.Linq.Extensions;
using Microsoft.EntityFrameworkCore;
using ActivityManagement.Authorization;
using ActivityManagement.Entities;
using ActivityManagement.Projects.Dto;

namespace ActivityManagement.Projects
{
    [AbpAuthorize(ActivityManagementPermissions.Projects.Default)]
    public class ProjectAppService : ActivityManagementAppServiceBase, IProjectAppService
    {
        private readonly IRepository<Project, long> _projectRepository;
        private readonly IRepository<ProjectEmployee, long> _projectEmployeeRepository;

        public ProjectAppService(
            IRepository<Project, long> projectRepository,
            IRepository<ProjectEmployee, long> projectEmployeeRepository)
        {
            _projectRepository = projectRepository;
            _projectEmployeeRepository = projectEmployeeRepository;
        }

        public async Task<PagedResultDto<ProjectDto>> GetAllAsync(GetProjectsInput input)
        {
            var query = _projectRepository.GetAll()
                .Include(p => p.Manager)
                .Include(p => p.ProjectEmployees)
                .Include(p => p.Tasks)
                .WhereIf(!string.IsNullOrWhiteSpace(input.Filter),
                    p => p.Name.Contains(input.Filter) || p.Code.Contains(input.Filter))
                .WhereIf(input.Status.HasValue, p => p.Status == input.Status.Value)
                .WhereIf(input.ManagerId.HasValue, p => p.ManagerId == input.ManagerId.Value);

            var count = await query.CountAsync();
            var items = await query.OrderByDescending(p => p.CreationTime).PageBy(input).ToListAsync();

            var dtos = items.Select(p => MapToProjectDto(p)).ToList();
            return new PagedResultDto<ProjectDto>(count, dtos);
        }

        public async Task<ProjectDto> GetAsync(long id)
        {
            var project = await _projectRepository.GetAll()
                .Include(p => p.Manager)
                .Include(p => p.ProjectEmployees).ThenInclude(pe => pe.Employee)
                .Include(p => p.Tasks)
                .FirstOrDefaultAsync(p => p.Id == id);

            return MapToProjectDto(project);
        }

        [AbpAuthorize(ActivityManagementPermissions.Projects.Create)]
        public async Task<ProjectDto> CreateAsync(CreateUpdateProjectDto input)
        {
            var project = ObjectMapper.Map<Project>(input);
            project.TenantId = AbpSession.TenantId ?? 1;
            await _projectRepository.InsertAsync(project);
            await CurrentUnitOfWork.SaveChangesAsync();
            return ObjectMapper.Map<ProjectDto>(project);
        }

        [AbpAuthorize(ActivityManagementPermissions.Projects.Edit)]
        public async Task<ProjectDto> UpdateAsync(CreateUpdateProjectDto input)
        {
            var project = await _projectRepository.GetAsync(input.Id);
            ObjectMapper.Map(input, project);
            return ObjectMapper.Map<ProjectDto>(project);
        }

        [AbpAuthorize(ActivityManagementPermissions.Projects.Delete)]
        public async Task DeleteAsync(long id)
        {
            await _projectRepository.DeleteAsync(id);
        }

        public async Task AddMemberAsync(long projectId, long employeeId, string role, bool isManager)
        {
            var existing = await _projectEmployeeRepository.FirstOrDefaultAsync(
                pe => pe.ProjectId == projectId && pe.EmployeeId == employeeId);
            if (existing == null)
            {
                await _projectEmployeeRepository.InsertAsync(new ProjectEmployee
                {
                    ProjectId = projectId,
                    EmployeeId = employeeId,
                    Role = role,
                    IsManager = isManager
                });
            }
        }

        public async Task RemoveMemberAsync(long projectId, long employeeId)
        {
            var pe = await _projectEmployeeRepository.FirstOrDefaultAsync(
                x => x.ProjectId == projectId && x.EmployeeId == employeeId);
            if (pe != null)
                await _projectEmployeeRepository.DeleteAsync(pe);
        }

        public async Task<ListResultDto<ProjectDto>> GetAllListAsync()
        {
            var projects = await _projectRepository.GetAll()
                .Include(p => p.Manager)
                .OrderBy(p => p.Name)
                .ToListAsync();
            return new ListResultDto<ProjectDto>(projects.Select(MapToProjectDto).ToList());
        }

        private ProjectDto MapToProjectDto(Project p)
        {
            var dto = ObjectMapper.Map<ProjectDto>(p);
            dto.StatusText = p.Status.ToString();
            dto.ManagerName = p.Manager?.FullName;
            dto.MemberCount = p.ProjectEmployees?.Count ?? 0;
            dto.TaskCount = p.Tasks?.Count ?? 0;
            dto.CompletedTaskCount = p.Tasks?.Count(t => t.Status == Entities.TaskStatus.Tamamlandi) ?? 0;
            dto.Members = p.ProjectEmployees?.Select(pe => new ProjectMemberDto
            {
                EmployeeId = pe.EmployeeId,
                FullName = pe.Employee?.FullName,
                Title = pe.Employee?.Title,
                Role = pe.Role,
                IsManager = pe.IsManager
            }).ToList() ?? new List<ProjectMemberDto>();
            return dto;
        }
    }
}
