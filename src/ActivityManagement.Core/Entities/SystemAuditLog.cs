using System;
using Abp.Domain.Entities;

namespace ActivityManagement.Entities
{
    // Merkezi sistem denetim kaydı (Create/Update/Delete). DbContext SaveChanges interceptor'ı otomatik yazar.
    public class SystemAuditLog : Entity<long>, IMayHaveTenant
    {
        public int? TenantId { get; set; }
        public long? UserId { get; set; }
        public string UserName { get; set; }
        public DateTime ExecutionTime { get; set; }
        public string ClientIpAddress { get; set; }
        public string ActionType { get; set; }   // Create / Update / Delete
        public string EntityName { get; set; }
        public string EntityId { get; set; }
        public string OriginalValues { get; set; } // JSON
        public string NewValues { get; set; }      // JSON
    }
}
