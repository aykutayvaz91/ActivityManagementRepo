using System.Collections.Generic;
using System.Threading.Tasks;
using Abp.Application.Services;
using ActivityManagement.Activities.Dto;
using ActivityManagement.Entities;
using ActivityManagement.ServiceRequests.Dto;

namespace ActivityManagement.ServiceRequests
{
    public interface IServiceRequestAppService : IApplicationService
    {
        Task<List<ServiceRequestDto>> GetAllAsync(GetServiceRequestsInput input);
        Task<ServiceRequestsIndexDto> GetIndexAsync(int cap = 500);
        Task<ServiceRequestDto> GetAsync(long id);
        Task<ServiceRequestDto> CreateAsync(CreateUpdateServiceRequestDto input);
        Task<ServiceRequestDto> UpdateAsync(CreateUpdateServiceRequestDto input);
        Task DeleteAsync(long id);

        // Talebi bir uzmana (ve opsiyonel yedeğe) ata — yönetici.
        Task<ServiceRequestDto> AssignAsync(long id, long? assignedEmployeeId, long? secondaryEmployeeId = null);
        // Durum + ilerleme güncelle — atanan kişi veya yönetici. Portal + write-back açıksa portala da POST edilir (note opsiyonel).
        Task<ServiceRequestDto> UpdateStatusAsync(long id, RequestStatus status, int percentage, string note = null);

        // (C13) Portal talebine yorum ekle → portala POST + yerel ayna. isInternal=false müşteriye e-posta tetikler.
        Task AddCommentAsync(long id, string body, bool isInternal);

        // Efor: yalnız atanan kişi kendi adına girer (faaliyet kuralı).
        Task<ActivityLogDto> LogEffortAsync(CreateActivityLogDto input);
        Task<List<ActivityLogDto>> GetEffortsAsync(long serviceRequestId);
        Task DeleteEffortAsync(long id);

        // Faz 2: Portaldan idempotent upsert (webhook alıcısı / sync). (Source, ExternalRef) anahtarı.
        // GÜVENLİK: dynamic API'ye AÇILMAZ — yalnız sunucu-içi (IntegrationController token'lı / HostedService) çağırır.
        [Abp.Application.Services.RemoteService(false)]
        Task<long> UpsertFromPortalAsync(PortalRequestDto input);

        // (C12) Portal DETAY aynası: talebin yorum + dosya + durumunu içe aktarır (dedup). Sunucu-içi çağrı.
        [Abp.Application.Services.RemoteService(false)]
        Task IngestPortalDetailAsync(PortalRequestDetailDto detail);

        // (C12) Portal dosya ekini sunucu-içi indirir (token'lı). Controller stream eder → dynamic API'ye açılmaz.
        [Abp.Application.Services.RemoteService(false)]
        Task<PortalFileDto> DownloadPortalAttachmentAsync(long requestId, long attachmentId);
    }
}
