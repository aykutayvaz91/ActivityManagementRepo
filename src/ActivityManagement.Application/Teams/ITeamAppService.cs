using System.Collections.Generic;
using System.Threading.Tasks;
using Abp.Application.Services;
using ActivityManagement.Teams.Dto;

namespace ActivityManagement.Teams
{
    public interface ITeamAppService : IApplicationService
    {
        Task<List<TeamDto>> GetAllAsync(bool onlyActive = false);
        Task<TeamDto> GetAsync(long id);
        Task<TeamDto> CreateAsync(CreateUpdateTeamDto input);
        Task<TeamDto> UpdateAsync(CreateUpdateTeamDto input);
        Task DeleteAsync(long id);
    }
}
