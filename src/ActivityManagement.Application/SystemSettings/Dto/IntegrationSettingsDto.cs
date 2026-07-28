using System;
using System.Collections.Generic;
using ActivityManagement.Entities;

namespace ActivityManagement.SystemSettings.Dto
{
    public class IntegrationSettingsDto
    {
        // GELEN (webhook)
        public bool HasInboundKey { get; set; }
        public string WebhookUrl { get; set; }   // portallara verilecek uç (gösterim)

        // GİDEN (pull) genel
        public bool SyncEnabled { get; set; }
        public int IntervalMinutes { get; set; }

        public List<IntegrationSourceDto> Sources { get; set; } = new List<IntegrationSourceDto>();
    }

    public class IntegrationSourceDto
    {
        public int Id { get; set; }
        public RequestSource Source { get; set; }
        public string SourceText { get; set; }
        public bool Enabled { get; set; }
        public string BaseUrl { get; set; }
        public bool HasApiKey { get; set; }      // anahtar ekranda gösterilmez, sadece "kayıtlı mı"
        public string AuthHeader { get; set; }
        public string AuthScheme { get; set; }
        public string UserEmail { get; set; }   // PSM X-User-Email aktör hesabı (varsa)
        public bool DetailSyncEnabled { get; set; }   // V2: talep detayı (yorum/dosya/durum) çek
        public bool WriteBackEnabled { get; set; }    // V2: yerelde durum/yorum → portala POST (DIŞA DÖNÜK, müşteriye e-posta)
        public string Filter { get; set; }
        public int InitialLookbackDays { get; set; }
        public DateTime? LastSyncUtc { get; set; }
        public DateTime? LastRunUtc { get; set; }
        public string LastResult { get; set; }
    }
}
