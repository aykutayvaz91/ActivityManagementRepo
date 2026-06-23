using System;
using System.Collections.Generic;
using Abp.Domain.Entities;
using Abp.Domain.Entities.Auditing;

namespace ActivityManagement.Entities
{
    public class Employee : FullAuditedEntity<long>, IMustHaveTenant
    {
        public int TenantId { get; set; }

        public long? UserId { get; set; }

        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string FullName => $"{FirstName} {LastName}";

        public string Title { get; set; }
        public string Department { get; set; }
        public string ExpertiseAreas { get; set; }
        public string PhotoUrl { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }

        public DateTime? BirthDate { get; set; }
        public DateTime HireDate { get; set; }

        public bool IsActive { get; set; } = true;

        public virtual ICollection<ProjectEmployee> ProjectEmployees { get; set; } = new List<ProjectEmployee>();
        public virtual ICollection<TaskItem> AssignedTasks { get; set; } = new List<TaskItem>();
        public virtual ICollection<ActivityLog> ActivityLogs { get; set; } = new List<ActivityLog>();
        public virtual ICollection<RoutineTask> RoutineTasks { get; set; } = new List<RoutineTask>();
        public virtual ICollection<Responsibility> Responsibilities { get; set; } = new List<Responsibility>();
    }
}
