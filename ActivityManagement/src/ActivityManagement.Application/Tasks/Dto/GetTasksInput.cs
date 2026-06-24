using Abp.Application.Services.Dto;
using ActivityManagement.Entities;

namespace ActivityManagement.Tasks.Dto
{
    public class GetTasksInput : PagedAndSortedResultRequestDto
    {
        public string Filter { get; set; }
        public long? ProjectId { get; set; }
        public long? AssignedEmployeeId { get; set; }
        public Entities.TaskStatus? Status { get; set; }
        public TaskPriority? Priority { get; set; }
        public string GroupName { get; set; }
    }
}
