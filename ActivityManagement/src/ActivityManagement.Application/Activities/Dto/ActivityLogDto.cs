using System;
using Abp.Application.Services.Dto;
using Abp.AutoMapper;
using ActivityManagement.Entities;

namespace ActivityManagement.Activities.Dto
{
    [AutoMapFrom(typeof(ActivityLog))]
    public class ActivityLogDto : EntityDto<long>
    {
        public int TenantId { get; set; }
        public long EmployeeId { get; set; }
        public string EmployeeName { get; set; }
        public long? TaskItemId { get; set; }
        public string TaskTitle { get; set; }
        public long? ProjectId { get; set; }
        public string ProjectName { get; set; }
        public string Description { get; set; }
        public DateTime ActivityDate { get; set; }
        public decimal HoursSpent { get; set; }
        public string ActivityType { get; set; }
        public DateTime CreationTime { get; set; }
    }

    public class CreateActivityLogDto
    {
        public long EmployeeId { get; set; }
        public long? TaskItemId { get; set; }
        public long? ProjectId { get; set; }
        public string Description { get; set; }
        public DateTime ActivityDate { get; set; } = DateTime.Today;
        public decimal HoursSpent { get; set; }
        public string ActivityType { get; set; } = "Genel";
    }

    public class GetActivitiesInput : PagedAndSortedResultRequestDto
    {
        public long? EmployeeId { get; set; }
        public long? ProjectId { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }
}
