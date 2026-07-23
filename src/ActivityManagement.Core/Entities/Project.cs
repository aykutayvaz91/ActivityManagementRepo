using System;
using System.Collections.Generic;
using Abp.Domain.Entities;
using Abp.Domain.Entities.Auditing;

namespace ActivityManagement.Entities
{
    public enum ProjectStatus
    {
        Planlandi = 0,
        Devam = 1,
        Tamamlandi = 2,
        Iptal = 3
    }

    public class Project : FullAuditedEntity<long>, IMustHaveTenant
    {
        public int TenantId { get; set; }

        public string Name { get; set; }
        public string Description { get; set; }
        public string Code { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public DateTime? PlannedEndDate { get; set; }

        // Proje SLA / hedef bitiş tarihi. Proje görevlerinde son tarih girilmezse buradan miras alınır.
        public DateTime? SlaTargetDate { get; set; }

        // Projenin ana/alt kategorisi. Proje görevleri/faaliyetleri bu kategorileri miras alır.
        public long? CategoryId { get; set; }
        public virtual Category Category { get; set; }

        public long? SubCategoryId { get; set; }
        public virtual SubCategory SubCategory { get; set; }

        public ProjectStatus Status { get; set; } = ProjectStatus.Planlandi;
        public int Priority { get; set; } = 1;

        // Eski tekli "Proje Yöneticisi" (ManagerId) yerine 1. ve 2. Sorumlu (her biri tek kişi)
        public long? ManagerId { get; set; }
        public virtual Employee Manager { get; set; }

        public long? PrimaryResponsibleId { get; set; }
        public virtual Employee PrimaryResponsible { get; set; }

        public long? SecondaryResponsibleId { get; set; }
        public virtual Employee SecondaryResponsible { get; set; }

        public long? TeamId { get; set; }
        public virtual Team Team { get; set; }

        public virtual ICollection<ProjectEmployee> ProjectEmployees { get; set; } = new List<ProjectEmployee>();
        public virtual ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
    }
}
