using Abp.Application.Services.Dto;
using ActivityManagement.Entities;

namespace ActivityManagement.Projects.Dto
{
    public class GetProjectsInput : PagedAndSortedResultRequestDto
    {
        public string Filter { get; set; }
        public ProjectStatus? Status { get; set; }
        public long? ManagerId { get; set; }
    }
}
