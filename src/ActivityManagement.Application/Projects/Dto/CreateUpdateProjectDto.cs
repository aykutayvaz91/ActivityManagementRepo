using System;
using System.ComponentModel.DataAnnotations;
using Abp.AutoMapper;
using ActivityManagement.Entities;

namespace ActivityManagement.Projects.Dto
{
    [AutoMapTo(typeof(Project))]
    [AutoMapFrom(typeof(ProjectDto))]
    public class CreateUpdateProjectDto
    {
        public long Id { get; set; }

        [Required]
        [MaxLength(128)]
        public string Name { get; set; }

        [MaxLength(2000)]
        public string Description { get; set; }

        [Required]
        [MaxLength(32)]
        public string Code { get; set; }

        [Required]
        public DateTime StartDate { get; set; } = DateTime.Today;

        public DateTime? PlannedEndDate { get; set; }
        public DateTime? EndDate { get; set; }

        // Proje SLA / hedef bitiş tarihi (zorunlu hedef)
        public DateTime? SlaTargetDate { get; set; }

        public ProjectStatus Status { get; set; } = ProjectStatus.Planlandi;
        public int Priority { get; set; } = 1;
        public long? ManagerId { get; set; }
        // 1. ve 2. Sorumlu (tekil kişi)
        public long? PrimaryResponsibleId { get; set; }
        public long? SecondaryResponsibleId { get; set; }
        // Zorunlu ana/alt kategori
        public long? CategoryId { get; set; }
        public long? SubCategoryId { get; set; }
        public long? TeamId { get; set; }
    }
}
