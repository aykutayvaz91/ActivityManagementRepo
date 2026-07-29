using System;
using System.ComponentModel.DataAnnotations;
using Abp.AutoMapper;
using ActivityManagement.Entities;

namespace ActivityManagement.Tasks.Dto
{
    [AutoMapTo(typeof(TaskItem))]
    [AutoMapFrom(typeof(TaskItemDto))]
    public class CreateUpdateTaskItemDto
    {
        public long Id { get; set; }

        // Eşzamanlılık (H3): formu açtığınız andaki değişiklik damgası (LastModificationTime/CreationTime ticks).
        // Kayıt sırasında DB daha yeni ise "başkası değiştirdi" uyarısı verilir (sessiz üzerine yazma önlenir).
        public long OriginalStamp { get; set; }

        [Required]
        [MaxLength(256)]
        public string Title { get; set; }

        [MaxLength(2000)]
        public string Description { get; set; }

        [MaxLength(256)]
        public string Category { get; set; }

        public long? SubCategoryId { get; set; }

        public long? ProjectId { get; set; }
        public long? AssignedEmployeeId { get; set; }
        public long? SecondaryEmployeeId { get; set; }
        public long? AssignedByEmployeeId { get; set; }

        public DateTime? StartDate { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime? CompletedDate { get; set; }

        public Entities.TaskStatus Status { get; set; } = Entities.TaskStatus.Beklemede;
        public TaskPriority Priority { get; set; } = TaskPriority.Normal;

        // Dinamik önem derecesi (1-10)
        [Range(1, 10)]
        public int PriorityScore { get; set; } = 5;

        public decimal EstimatedHours { get; set; }
        public decimal ActualHours { get; set; }
        public int CompletionPercentage { get; set; }

        // Görev grubu
        public string GroupName { get; set; }

        // Aktivite tipi
        public Entities.ActivityType? ActivityType { get; set; }
    }
}
