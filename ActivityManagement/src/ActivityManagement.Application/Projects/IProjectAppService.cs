using System.Threading.Tasks;
using Abp.Application.Services;
using Abp.Application.Services.Dto;
using ActivityManagement.Projects.Dto;

namespace ActivityManagement.Projects
{
    public interface IProjectAppService : IApplicationService
    {
        Task<PagedResultDto<ProjectDto>> GetAllAsync(GetProjectsInput input);
        Task<ProjectDto> GetAsync(long id);
        Task<ProjectDto> CreateAsync(CreateUpdateProjectDto input);
        Task<ProjectDto> UpdateAsync(CreateUpdateProjectDto input);
        Task DeleteAsync(long id);
        Task AddMemberAsync(long projectId, long employeeId, string role, bool isManager);
        Task RemoveMemberAsync(long projectId, long employeeId);
        Task<ListResultDto<ProjectDto>> GetAllListAsync();
    }
}
