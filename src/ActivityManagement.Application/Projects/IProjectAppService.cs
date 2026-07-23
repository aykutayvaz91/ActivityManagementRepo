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
        // responsibilityLevel: 0=Üye, 1=1. Sorumlu, 2=2. Sorumlu
        Task AddMemberAsync(long projectId, long employeeId, string role, bool isManager, int responsibilityLevel = 0);
        Task RemoveMemberAsync(long projectId, long employeeId);
        Task<ListResultDto<ProjectDto>> GetAllListAsync();
        // Sıradaki otomatik proje kodu (PRJ-### — mevcut en büyük numaradan +1)
        Task<string> GetNextCodeAsync();
    }
}
