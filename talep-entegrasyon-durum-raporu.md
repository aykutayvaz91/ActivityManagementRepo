# Talep (ServiceRequest) + Portal Entegrasyon — Durum Raporu

**Tarih:** 2026-07-28 · **Kapsam:** Talep senkron/yaşam döngüsü, pull istemcisi, görünürlük, performans, v2 (yorum/dosya/write-back). Kod okundu, değiştirilmedi.

---

## 🔴 A) Yüksek öncelik (bug / veri kaybı riski)

1. **Pull: `updatedSince` + `from` aynı istekte gönderiliyor** — `RequestSyncHostedService.cs:186-188`
   PSM için eklenen `from` (oluşturma tarihi) tüm kaynaklara gidiyor. Portal ikisini **AND**'lerse: watermark'tan önce oluşup sonra güncellenen talepler `from` filtresine takılır → **durum değişimleri kaçar** (sistemin ana amacı). Çözüm: kaynak başına tek tarih parametresi (config `DateParamMode`).

2. **`GetAllAsync` sayfalamayı (MaxResultCount/Sorting) tamamen yok sayıyor** — `ServiceRequestAppService.cs:142-177`
   `WhereIf` sonrası doğrudan `ToListAsync()`. Query'deki 1000, Export'taki 10000 **etkisiz**; her çağrı **tüm** kümeyi çeker. 3000+ talepte ağır.

3. **Talepler ana ekranı her açılışta 2× tam yükleme + `Include(Logs)` + bellek filtresi** — `RequestsController.cs:101-108`, `ServiceRequestAppService.cs:131-138`
   Sunucu+Destek tüm talepler + tüm efor kayıtları belleğe, sonra `IsArchived` bellek filtresi. Arşiv ayrımı SQL'e, `Logs` yerine `Sum/Count` projeksiyonuna taşınmalı.

4. **Eşlenemeyen portal durumu sessizce kayboluyor** — `ServiceRequestAppService.cs:544-556`
   `MapStatusText` tanımadığı metinde `null` → yeni kayıt `Yeni/Atandı`'ya düşer, gerçek durum kaybolur; sonraki senkronlarda da düzelmez. En az `ErrorLog`'a yazılmalı (bilinmeyen durumları görüp eşleyelim).

5. **Sayfa boyutu isteğe eklenmiyor ama `<50` ile duruluyor → eksik çekme** — `RequestSyncHostedService.cs:177,184-190`
   Portal sayfa başı <50 döndürürse ilk sayfadan sonra döngü kırılır, kalan sayfalar kaçar. `&pageSize=50` gönderilmeli. Ayrıca 50-sayfa cap (~2500) + watermark ilerlemesi büyük backfill'de kayıp.

## 🟡 B) Orta öncelik (tutarlılık / veri)

6. **İptal talepler /Work'te "efor bekliyor" diye nag + aktif sekmede kalıyor** — `WorkController.cs:77-87`, `RequestsController.cs:104`
   İptal → IsOpen=false + efor=0 → "İptal · efor bekliyor" (yanlış). İptal için efor beklenmemeli; İptal de arşive/dışına alınmalı.

7. **Yalnız "Çözüldü" gönderen portalda arşiv hiç dolmaz** — `RequestsController.cs:104`
   `IsArchived = Kapandi && efor>0`. Portal ayrı "Closed" göndermezse eforu girilmiş bitmiş talepler sonsuza dek aktif kalır. Arşiv kuralı Çözüldü'yü de kapsamalı.

8. **DB'de `(Source, ExternalRef)` UNIQUE değil + anahtarsız kayıt çoğaltma** — `ActivityManagementDbContext.cs:316-317`, `ServiceRequestAppService.cs:407-443`
   Non-unique indeks → webhook+HostedService yarışında çift kayıt. ExternalRef+id boş öğe her turda çoğalır. Çözüm: `.IsUnique().HasFilter("[ExternalRef] IS NOT NULL")`.

9. **İlk backfill'de açık+atanmış geçmiş talepler için bildirim seli** — `ServiceRequestAppService.cs:490-496`
   `isNew` filtresi yalnız kapalıları eler; ilk sync'te penceredeki tüm açık+atanmış talepler zil gönderir. İlk-yükleme sessiz olmalı (ör. ReceivedDate eski ise bildirme).

10. **TakımLideri kendi takımının taleplerini listede göremiyor ama yönetebiliyor** — `ServiceRequestAppService.cs:163-168` vs `123-129`
    Gör/yönet asimetrisi: havuzdaki takım taleplerini listede göremez (yalnız Detail URL). Karar: TakımLideri takım taleplerini görsün mü?

11. **2. sorumluya durum butonu görünür ama submit reddedilir** — `_RequestTable.cshtml:98` vs `ServiceRequestAppService.cs:296-299`
    UI/servis uyumsuzluğu (manuel talepte).

## 🟢 C) v2 — eksik özellikler (yeni geliştirme)

12. **Talepte yorum (Comment) + dosya (Attachment) saklama/gösterme YOK** — `ServiceRequest.cs:84` (yalnız `Logs`)
    destek-entegrasyon-v2.md'de istenen yorum/dosya çekme için önce **saklama (entity+migration) + Detail'de gösterim** gerekli. (Sözleşmeden bağımsız, şimdi yapılabilir/test edilebilir.)

13. **Write-back (portala POST) altyapısı YOK** — entegrasyon tek yönlü (yalnız pull + inbound webhook)
    Durum/yorum push için giden istemci yok. destek uçları henüz **yok** (v2 doküman iletildi); PSM'in yazma uçları spec'te var. HTTP tarafı ancak karşı uç canlı olunca uçtan uca doğrulanır.

## ⚪ D) Genel backlog (CLAUDE.md)
- Faz 3: görev onay mekanizması (rich text yorum zaten var).
- Faz 4: Kişi kartı 360°, Gantt, gelişmiş Excel/PDF raporlama.

---

## Öneri (sıra)
1. **A grubu** (bug/performans) — küçük, yüksek etkili, test edilebilir. Önce bunlar.
2. **B grubu** (İptal/Çözüldü arşiv, unique index, bildirim seli) — tutarlılık.
3. **C-12** (yorum/dosya saklama+gösterme) — sözleşmeden bağımsız, şimdi yapılıp test edilebilir; destek yorumları gönderince hazır olur.
4. **C-13 write-back + detay HTTP** — destek/PSM uçları netleşince (komut deseni), tek doğrulanabilir pass.
