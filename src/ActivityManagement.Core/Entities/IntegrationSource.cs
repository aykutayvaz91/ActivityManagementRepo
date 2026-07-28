using System;
using Abp.Domain.Entities;

namespace ActivityManagement.Entities
{
    // Kaynak başına (Sunucu/Destek) PULL entegrasyon yapılandırması. Admin panelinden yönetilir.
    // Seed'de her RequestSource için birer satır oluşturulur (varsayılan kapalı).
    public class IntegrationSource : Entity<int>, IMustHaveTenant
    {
        public int TenantId { get; set; }

        public RequestSource Source { get; set; }

        public bool Enabled { get; set; } = false;

        // Portalın okuma ucu (liste API'si). Örn: https://psm.tdv.org/api/kurulum-talepleri
        public string BaseUrl { get; set; }

        // GİDEN token (portala gönderdiğimiz). EmailSettings deseni: boş bırakılırsa değişmez.
        public string ApiKey { get; set; }

        // Kimlik header'ı ve şeması. Örn header="Authorization", scheme="Bearer" → "Authorization: Bearer <key>".
        // scheme boşsa header'a doğrudan anahtar yazılır (örn header="X-Api-Key").
        public string AuthHeader { get; set; } = "Authorization";
        public string AuthScheme { get; set; } = "Bearer";

        // İkinci kimlik/aktör header'ı (PSM: "X-User-Email"). Doluysa her isteğe "X-User-Email: <UserEmail>" eklenir.
        // Boşsa gönderilmez (destek portalı kullanmaz).
        public string UserEmail { get; set; }

        // Ek sorgu parametreleri (grup/atanan filtresi vb.). Örn: "group=Sistem ve Altyapı Operasyon".
        public string Filter { get; set; }

        // V2: Talep DETAYINI (yorum + dosya + güncel durum) çeker. Portalın detay ucu (GET .../{talepNo})
        // açık olan kaynaklarda açılır. Kapalıysa yalnız liste (meta) senkronu yapılır.
        public bool DetailSyncEnabled { get; set; } = false;

        // V2: Çift yönlü — yerelde durum/yorum değişince portala POST (write-back). Portalın yazma uçları
        // açık VE yönetici onayı olan kaynaklarda açılır. DIŞA-DÖNÜK (müşteriye e-posta tetikler) → varsayılan KAPALI.
        public bool WriteBackEnabled { get; set; } = false;

        // İlk çalıştırmada / watermark yokken kaç gün geriye bakılacağı.
        public int InitialLookbackDays { get; set; } = 7;

        // Artımlı senkron damgası (son başarıyla çekilen "updatedSince").
        public DateTime? LastSyncUtc { get; set; }
        // Son çalıştırma zamanı ve sonucu (admin ekranında durum göstergesi).
        public DateTime? LastRunUtc { get; set; }
        public string LastResult { get; set; }
    }
}
