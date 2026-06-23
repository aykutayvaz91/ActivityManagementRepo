using System;
using System.Collections.Generic;
using Abp.Application.Services.Dto;
using Abp.AutoMapper;
using ActivityManagement.Entities;

namespace ActivityManagement.Employees.Dto
{
    [AutoMapFrom(typeof(Employee))]
    public class EmployeeDto : FullAuditedEntityDto<long>
    {
        public int TenantId { get; set; }
        public long? UserId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string FullName => $"{FirstName} {LastName}";
        public string Title { get; set; }
        public string Department { get; set; }
        public string AppRole { get; set; }
        public string ExpertiseAreas { get; set; }
        public string PhotoUrl { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public DateTime? BirthDate { get; set; }
        public DateTime HireDate { get; set; }
        public bool IsActive { get; set; }

        public List<ResponsibilityDto> Responsibilities { get; set; } = new List<ResponsibilityDto>();
        public List<AssignedProjectDto> AssignedProjects { get; set; } = new List<AssignedProjectDto>();
        public int PendingTaskCount { get; set; }
        public int InProgressTaskCount { get; set; }
        public int CompletedTaskCount { get; set; }
    }

    public class ResponsibilityDto
    {
        public long Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public int OrderNo { get; set; }
    }

    public class AssignedProjectDto
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public string Code { get; set; }
        public string Role { get; set; }
        public string Status { get; set; }
    }
}
