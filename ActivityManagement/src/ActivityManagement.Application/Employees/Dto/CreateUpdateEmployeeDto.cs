using System;
using System.ComponentModel.DataAnnotations;
using Abp.AutoMapper;
using ActivityManagement.Entities;

namespace ActivityManagement.Employees.Dto
{
    [AutoMapTo(typeof(Employee))]
    [AutoMapFrom(typeof(EmployeeDto))]
    public class CreateUpdateEmployeeDto
    {
        public long Id { get; set; }

        [Required]
        [MaxLength(64)]
        public string FirstName { get; set; }

        [Required]
        [MaxLength(64)]
        public string LastName { get; set; }

        [MaxLength(128)]
        public string Title { get; set; }

        [MaxLength(128)]
        public string Department { get; set; }

        [MaxLength(32)]
        public string AppRole { get; set; } = "Uzman";

        [MaxLength(500)]
        public string ExpertiseAreas { get; set; }

        [MaxLength(256)]
        public string PhotoUrl { get; set; }

        [EmailAddress]
        [MaxLength(256)]
        public string Email { get; set; }

        [MaxLength(32)]
        public string Phone { get; set; }

        public DateTime? BirthDate { get; set; }

        [Required]
        public DateTime HireDate { get; set; } = DateTime.Today;

        public bool IsActive { get; set; } = true;

        public long? UserId { get; set; }
    }
}
