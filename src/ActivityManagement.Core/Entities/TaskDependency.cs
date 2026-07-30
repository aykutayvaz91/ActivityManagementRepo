using Abp.Domain.Entities;

namespace ActivityManagement.Entities
{
    // Görev bağımlılığı (bitiş→başlangıç): TaskItemId, DependsOnTaskId bittikten SONRA başlar.
    // Gantt'ta ok olarak çizilir; kritik yol (CPM) hesabında kenar olarak kullanılır.
    public class TaskDependency : Entity<long>, IMustHaveTenant
    {
        public int TenantId { get; set; }
        public long TaskItemId { get; set; }        // ardıl (successor) — bağımlı olan
        public long DependsOnTaskId { get; set; }   // öncül (predecessor) — önce bitmesi gereken
    }
}
