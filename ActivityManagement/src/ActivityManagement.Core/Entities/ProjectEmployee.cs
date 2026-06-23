using Abp.Domain.Entities;

namespace ActivityManagement.Entities
{
    public class ProjectEmployee : Entity<long>
    {
        public long ProjectId { get; set; }
        public virtual Project Project { get; set; }

        public long EmployeeId { get; set; }
        public virtual Employee Employee { get; set; }

        public string Role { get; set; }
        public bool IsManager { get; set; }
    }
}
