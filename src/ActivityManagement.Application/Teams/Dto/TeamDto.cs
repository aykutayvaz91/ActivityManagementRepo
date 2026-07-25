using Abp.Application.Services.Dto;

namespace ActivityManagement.Teams.Dto
{
    public class TeamDto : FullAuditedEntityDto<long>
    {
        public string Name { get; set; }
        public string ShortName { get; set; }
        public string Description { get; set; }
        public long? LeaderId { get; set; }
        public string LeaderName { get; set; }
        public bool IsActive { get; set; }
        public int MemberCount { get; set; }
        public int ProjectCount { get; set; }
    }

    public class CreateUpdateTeamDto
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public string ShortName { get; set; }
        public string Description { get; set; }
        public long? LeaderId { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
