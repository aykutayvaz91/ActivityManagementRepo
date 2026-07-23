using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Abp.Domain.Repositories;
using Abp.UI;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ActivityManagement.Entities;
using ActivityManagement.Teams.Dto;

namespace ActivityManagement.Teams
{
    // Takım oluşturma/düzenleme/silme yapısal bir admin işlemidir: sadece Admin yapabilir.
    public class TeamAppService : ActivityManagementAppServiceBase, ITeamAppService
    {
        private readonly IRepository<Team, long> _teamRepository;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public TeamAppService(IRepository<Team, long> teamRepository, IHttpContextAccessor httpContextAccessor)
        {
            _teamRepository = teamRepository;
            _httpContextAccessor = httpContextAccessor;
        }

        private void EnsureAdmin()
        {
            var role = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Role)?.Value ?? "Uzman";
            if (!string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
                throw new UserFriendlyException("Takım oluşturma/düzenleme/silme yetkiniz yok. Yalnızca Admin yapabilir.");
        }

        public async Task<List<TeamDto>> GetAllAsync(bool onlyActive = false)
        {
            IQueryable<Team> query = _teamRepository.GetAll()
                .Include(t => t.Leader)
                .Include(t => t.Members)
                .Include(t => t.Projects);

            if (onlyActive) query = query.Where(t => t.IsActive);

            var list = await query.OrderBy(t => t.Name).ToListAsync();
            return list.Select(MapToDto).ToList();
        }

        public async Task<TeamDto> GetAsync(long id)
        {
            var team = await _teamRepository.GetAll()
                .Include(t => t.Leader)
                .Include(t => t.Members)
                .Include(t => t.Projects)
                .FirstOrDefaultAsync(t => t.Id == id);
            return team == null ? null : MapToDto(team);
        }

        public async Task<TeamDto> CreateAsync(CreateUpdateTeamDto input)
        {
            EnsureAdmin();
            var team = new Team
            {
                Name = input.Name,
                Description = input.Description,
                LeaderId = input.LeaderId,
                IsActive = input.IsActive
            };
            await _teamRepository.InsertAsync(team);
            await CurrentUnitOfWork.SaveChangesAsync();
            return await GetAsync(team.Id);
        }

        public async Task<TeamDto> UpdateAsync(CreateUpdateTeamDto input)
        {
            EnsureAdmin();
            var team = await _teamRepository.GetAsync(input.Id);
            team.Name = input.Name;
            team.Description = input.Description;
            team.LeaderId = input.LeaderId;
            team.IsActive = input.IsActive;
            await CurrentUnitOfWork.SaveChangesAsync();
            return await GetAsync(team.Id);
        }

        public async Task DeleteAsync(long id)
        {
            EnsureAdmin();
            await _teamRepository.DeleteAsync(id);
        }

        private static TeamDto MapToDto(Team t)
        {
            return new TeamDto
            {
                Id = t.Id,
                Name = t.Name,
                Description = t.Description,
                LeaderId = t.LeaderId,
                LeaderName = t.Leader?.FullName,
                IsActive = t.IsActive,
                MemberCount = t.Members?.Count ?? 0,
                ProjectCount = t.Projects?.Count ?? 0
            };
        }
    }
}
