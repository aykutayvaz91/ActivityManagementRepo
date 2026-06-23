using Abp.Application.Services.Dto;

namespace ActivityManagement.Employees.Dto
{
    public class GetEmployeesInput : PagedAndSortedResultRequestDto
    {
        public string Filter { get; set; }
        public string Department { get; set; }
        public bool? IsActive { get; set; }
    }
}
