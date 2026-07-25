using System;
using Abp.Application.Services.Dto;
using Abp.AutoMapper;
using ActivityManagement.Entities;

namespace ActivityManagement.ServiceRequests.Dto
{
    [AutoMapFrom(typeof(ServiceRequest))]
    public class ServiceRequestDto : FullAuditedEntityDto<long>
    {
        public RequestSource Source { get; set; }
        public string SourceText { get; set; }
        public string ExternalRef { get; set; }
        public string ExternalUrl { get; set; }

        public string Title { get; set; }
        public string Description { get; set; }
        public string ActivityType { get; set; }

        public string RequesterName { get; set; }
        public string RequesterEmail { get; set; }
        public string ExtraInfo { get; set; }

        public RequestStatus Status { get; set; }
        public string StatusText { get; set; }

        public TaskPriority Priority { get; set; }
        public int PriorityScore { get; set; }

        public long? AssignedEmployeeId { get; set; }
        public string AssignedEmployeeName { get; set; }
        public long? SecondaryEmployeeId { get; set; }
        public string SecondaryEmployeeName { get; set; }
        public long? TeamId { get; set; }
        public string TeamName { get; set; }

        public long? CategoryId { get; set; }
        public string CategoryName { get; set; }
        public long? SubCategoryId { get; set; }
        public string SubCategoryName { get; set; }
        public long? ProjectId { get; set; }
        public string ProjectName { get; set; }

        public DateTime? ReceivedDate { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime? ResolvedDate { get; set; }
        public DateTime? ClosedDate { get; set; }

        public int CompletionPercentage { get; set; }

        // Özet
        public int LogCount { get; set; }
        public decimal TotalHours { get; set; }

        // Sunucu tarafı yetki
        public bool CanManage { get; set; }     // ata/durum/düzenle (yönetici veya atanan kişi)
        public bool CanLogEffort { get; set; }  // efor gir (yalnız atanan kişi)

        // Görsel yardımcılar
        public bool IsOpen { get; set; }
        public bool IsOverdue { get; set; }
    }

    // Manuel oluşturma/düzenleme (portal entegrasyonu gelmeden de talep girilebilir).
    public class CreateUpdateServiceRequestDto
    {
        public long Id { get; set; }
        public RequestSource Source { get; set; } = RequestSource.SunucuKurulum;
        public string ExternalRef { get; set; }
        public string ExternalUrl { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string ActivityType { get; set; }
        public string RequesterName { get; set; }
        public string RequesterEmail { get; set; }
        public string ExtraInfo { get; set; }
        public TaskPriority Priority { get; set; } = TaskPriority.Normal;
        public int PriorityScore { get; set; } = 5;
        public long? AssignedEmployeeId { get; set; }
        public long? SecondaryEmployeeId { get; set; }
        public long? ProjectId { get; set; }
        public DateTime? ReceivedDate { get; set; }
        public DateTime? DueDate { get; set; }
    }

    public class GetServiceRequestsInput : PagedAndSortedResultRequestDto
    {
        public RequestSource? Source { get; set; }
        public RequestStatus? Status { get; set; }
        public long? AssignedEmployeeId { get; set; }
        public bool? OnlyOpen { get; set; }   // Kapandı/İptal hariç
        public bool? MineOnly { get; set; }   // yalnız bana atanan
        public string Filter { get; set; }
    }

    // Faz 2: Portaldan gelen normalize talep (webhook alıcısı / sync kullanır). Idempotent upsert.
    public class PortalRequestDto
    {
        public RequestSource Source { get; set; }
        public string ExternalRef { get; set; }
        public string ExternalUrl { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string ActivityType { get; set; }
        public string RequesterName { get; set; }
        public string RequesterEmail { get; set; }
        public string ExtraInfo { get; set; }

        // Sunucu tarafında eşlenecek ham alanlar:
        public string AssigneeEmail { get; set; }   // → Employee (e-posta ile)
        public string GroupName { get; set; }        // → Team (ad ile)
        public string StatusText { get; set; }       // → RequestStatus (metin eşleme)
        public string PriorityText { get; set; }     // → PriorityScore (metin eşleme)

        public int? PriorityScore { get; set; }      // sayısal verilirse öncelikli
        public DateTime? ReceivedDate { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime? ResolvedDate { get; set; }
        // Enum doğrudan verilirse StatusText yerine kullanılır (webhook doğrudan gönderebilir).
        public RequestStatus? Status { get; set; }
    }
}
