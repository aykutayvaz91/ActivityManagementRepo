using System;
using System.Collections.Generic;
using Abp.Application.Services.Dto;
using Abp.AutoMapper;
using ActivityManagement.Entities;

namespace ActivityManagement.Projects.Dto
{
    [AutoMapFrom(typeof(Project))]
    public class ProjectDto : FullAuditedEntityDto<long>
    {
        public int TenantId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Code { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public DateTime? PlannedEndDate { get; set; }
        public DateTime? SlaTargetDate { get; set; }
        public ProjectStatus Status { get; set; }
        public string StatusText { get; set; }
        public int Priority { get; set; }
        public long? ManagerId { get; set; }
        public string ManagerName { get; set; }
        public long? PrimaryResponsibleId { get; set; }
        public string PrimaryResponsibleName { get; set; }
        public long? SecondaryResponsibleId { get; set; }
        public string SecondaryResponsibleName { get; set; }
        public long? CategoryId { get; set; }
        public string CategoryName { get; set; }
        public long? SubCategoryId { get; set; }
        public string SubCategoryName { get; set; }
        public long? TeamId { get; set; }
        public string TeamName { get; set; }
        public int MemberCount { get; set; }
        public int TaskCount { get; set; }
        public int CompletedTaskCount { get; set; }
        public List<ProjectMemberDto> Members { get; set; } = new List<ProjectMemberDto>();
    }

    public class ProjectMemberDto
    {
        public long EmployeeId { get; set; }
        public string FullName { get; set; }
        public string Title { get; set; }
        public string Role { get; set; }
        public bool IsManager { get; set; }
    }
}
