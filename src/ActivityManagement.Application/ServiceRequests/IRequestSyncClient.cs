using System.Collections.Generic;
using System.Threading.Tasks;
using ActivityManagement.Entities;
using ActivityManagement.ServiceRequests.Dto;

namespace ActivityManagement.ServiceRequests
{
    // FAZ 2 — Portal entegrasyonunun PULL (çekme) alternatifi için sözleşme.
    // Push (webhook alıcısı: IntegrationController) varsayılan yoldur. Portallar webhook atamıyorsa,
    // bu arabirimin bir uyarlaması periyodik olarak portal API'sinden yeni talepleri çeker ve
    // IServiceRequestAppService.UpsertFromPortalAsync ile idempotent aktarır.
    //
    // Not: Portal API bilgisi (endpoint/auth) netleşince bir uyarlama + zamanlanmış HostedService eklenecek.
    // Şimdilik tanım vardır ancak çalışan bir uyarlama KAYITLI DEĞİLDİR (entegrasyon kapalı).
    public interface IRequestSyncClient
    {
        RequestSource Source { get; }
        // Portaldan (varsa) yeni/güncel talepleri normalize DTO olarak getirir.
        Task<List<PortalRequestDto>> PullAsync();
    }
}
