using Abp.Domain.Entities;

namespace ActivityManagement.Entities
{
    // Yapılandırılabilir görev durumu (Kanban kolonları). Yönetici düzenler.
    // StatusValue, TaskItem.Status (TaskStatus enum) tamsayı değeriyle eşleşir.
    public class WorkflowStatus : Entity<int>
    {
        public string Name { get; set; }
        public string Color { get; set; }      // bootstrap rengi: secondary/primary/success/dark/warning...
        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsCompletedState { get; set; }  // bu duruma geçince tamamlanma tarihi set edilir
        public int StatusValue { get; set; }   // TaskStatus enum karşılığı (0..4)
    }
}
