using System;
using System.Collections.Generic;

namespace ActivityManagement.Tasks.Dto
{
    // Gantt satırı: frappe-gantt için gereken alanlar + bağımlılık + kritik yol işareti.
    public class GanttTaskDto
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public int Progress { get; set; }
        public int Status { get; set; }
        public string DependsOn { get; set; }   // virgülle ayrık öncül id'ler (frappe "dependencies")
        public bool IsCritical { get; set; }     // kritik yolda mı (CPM, slack=0)
    }

    // Görev detayında bağımlılık yönetimi için.
    public class TaskDependencyInfoDto
    {
        public long TaskId { get; set; }
        public bool CanManage { get; set; }
        public List<DependencyItemDto> Predecessors { get; set; } = new();  // bu görevin öncülleri
        public List<DependencyItemDto> Candidates { get; set; } = new();    // eklenebilecek görevler
    }

    public class DependencyItemDto
    {
        public long Id { get; set; }
        public string Title { get; set; }
    }
}
