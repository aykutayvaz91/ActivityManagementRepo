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

        // Portal aynası (salt-okunur) — GetAsync/Detail'de doldurulur
        public System.Collections.Generic.List<RequestCommentDto> Comments { get; set; } = new();
        public System.Collections.Generic.List<RequestAttachmentDto> Attachments { get; set; } = new();
    }

    [AutoMapFrom(typeof(ServiceRequestComment))]
    public class RequestCommentDto
    {
        public string AuthorName { get; set; }
        public string AuthorEmail { get; set; }
        public DateTime? CommentDate { get; set; }
        public string Body { get; set; }
        public bool IsInternal { get; set; }
    }

    [AutoMapFrom(typeof(ServiceRequestAttachment))]
    public class RequestAttachmentDto
    {
        public long Id { get; set; }            // yerel ek kimliği — token'lı indirme proxy linki için
        public string FileName { get; set; }
        public string Url { get; set; }
        public long SizeBytes { get; set; }
        public string ContentType { get; set; }
        public DateTime? UploadedAt { get; set; }
    }

    // Token'lı portal dosyasını sunucu-içi indirip tarayıcıya akıtmak için (proxy).
    public class PortalFileDto
    {
        public byte[] Content { get; set; }
        public string ContentType { get; set; }
        public string FileName { get; set; }
    }

    // Portal DETAY yanıtı (GET /api/talepler/{id}) — yorum + dosya + durum. Ingest için.
    public class PortalRequestDetailDto
    {
        public RequestSource Source { get; set; }
        public string ExternalRef { get; set; }
        public string StatusText { get; set; }
        public System.Collections.Generic.List<PortalCommentDto> Comments { get; set; } = new();
        public System.Collections.Generic.List<PortalAttachmentDto> Attachments { get; set; } = new();
    }
    public class PortalCommentDto
    {
        public string Id { get; set; }
        public string Author { get; set; }
        public string AuthorEmail { get; set; }
        public DateTime? Date { get; set; }
        public string Body { get; set; }
        public bool IsInternal { get; set; }
    }
    public class PortalAttachmentDto
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Url { get; set; }
        public long SizeBytes { get; set; }
        public string ContentType { get; set; }
        public DateTime? UploadedAt { get; set; }
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
        // Varsayılan: sınırsız (ABP'nin 10 varsayılanı yerine). Sayfalama isteyen açıkça küçültür (Query/Export).
        public GetServiceRequestsInput() { MaxResultCount = 100000; }

        public RequestSource? Source { get; set; }
        public RequestStatus? Status { get; set; }
        public long? AssignedEmployeeId { get; set; }
        public bool? OnlyOpen { get; set; }     // Kapandı/İptal hariç
        public bool? MineOnly { get; set; }     // yalnız bana atanan
        public bool? OnlyNoEffort { get; set; } // yalnız eforu girilmemiş (rapor boşluğu)
        public string Filter { get; set; }
    }

    // Talepler ana ekranı (Index) — verimli: sekme başına SINIRLI liste + gerçek SQL sayaçları.
    public class ServiceRequestsIndexDto
    {
        public System.Collections.Generic.List<ServiceRequestDto> ActiveSunucu { get; set; } = new();
        public System.Collections.Generic.List<ServiceRequestDto> ActiveDestek { get; set; } = new();
        public System.Collections.Generic.List<ServiceRequestDto> Archived { get; set; } = new();
        public int CountSunucu { get; set; }
        public int CountDestek { get; set; }
        public int CountArchived { get; set; }
        public int Cap { get; set; }
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
