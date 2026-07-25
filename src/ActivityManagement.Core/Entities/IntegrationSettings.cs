using Abp.Domain.Entities;

namespace ActivityManagement.Entities
{
    // Tek satırlık (Id=1) entegrasyon genel ayarı. Admin panelinden yönetilir.
    public class IntegrationSettings : Entity<int>, IMustHaveTenant
    {
        public int TenantId { get; set; }

        // GELEN (webhook): portalların POST atarken kullanacağı API anahtarı. Boşsa alıcı endpoint 503 (kapalı).
        public string InboundApiKey { get; set; }

        // GİDEN (pull): periyodik senkron ana anahtarı. Kapalıysa hiçbir kaynak çekilmez.
        public bool SyncEnabled { get; set; } = false;

        // Senkron döngü aralığı (dakika).
        public int IntervalMinutes { get; set; } = 10;
    }
}
