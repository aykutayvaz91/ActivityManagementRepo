using System;
using Abp.Application.Services.Dto;
using ActivityManagement.Entities;

namespace ActivityManagement.Tasks.Dto
{
    public class GetTasksInput : PagedAndSortedResultRequestDto
    {
        public string Filter { get; set; }
        public long? ProjectId { get; set; }
        public long? CategoryId { get; set; }
        public long? SubCategoryId { get; set; }
        public long? TeamId { get; set; }
        public DateTime? CompletedFrom { get; set; }
        public DateTime? CompletedTo { get; set; }
        // true: gecikmeli tamamlanan, false: zamanında tamamlanan
        public bool? IsLate { get; set; }
        public long? AssignedEmployeeId { get; set; }
        public Entities.TaskStatus? Status { get; set; }
        public TaskPriority? Priority { get; set; }
        public Entities.ActivityType? ActivityType { get; set; } // Görev tipi filtresi (V4)
        public string GroupName { get; set; }
    }
}
