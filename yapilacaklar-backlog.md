# Yapılacaklar — Backlog

> DURUM (2026-07-29 sonu): Y1/Y2/Y3 + denetim ORTA + DÜŞÜK maddeleri UYGULANDI (v1.17.2→1.17.5, canlı). Kalanlar aşağıda **[BEKLİYOR]** ile işaretli. "Devam et"te bu dosya + `history/2026-07-29.md` okunmalı.

## ✅ TAMAMLANANLAR (2026-07-29, canlı)
- **Y1** İç not + resim: portal reddederse (destek multipart bug) iç not YEREL saklanır. (v1.17.2)
- **Y2** Faaliyetler: Admin/Manager/TakımLideri tümü, Uzman kendi+atanan; server-side arama. (v1.17.2)
- **Y3** Talep: Çözüldü ≠ Kapatılan; yalnız Kapandı+efor → arşiv. (v1.17.2)
- **6 KRİTİK GÜVENLİK** (v1.17.1): dosya-yükleme XSS, talep-detay IDOR, rapor IDOR, SafeHtml sanitizer, portal-ek SSRF, Tasks CSRF.
- **ORTA:** EmployeeAppService takım-kapsamı; sync watermark sırası+detay hata sayacı; PortalStatusText normalize; yorum çift-kayıt; GetTeamReport N+1; ProjectAppService+ReportAppService AsNoTracking; Tasks Edit/Delete null-safety+try/catch; TaskItem AddComment async I/O; /Reports 500 fix. (v1.17.3–1.17.4)
- **DÜŞÜK:** güvenlik başlıkları (X-Frame/nosniff/Referrer); admin login sabit-zaman + fallback parola kaldırıldı; webhook X-Api-Key sabit-zaman; AccessControl okuma RemoteService(false); çift HoursSpent kaldırıldı. (v1.17.5)

## [BEKLİYOR] — kalan işler

### Y5 — Destek vs PSM talep DETAY ayrı arayüz  [BEKLİYOR]
- Destek: `/Requests/Detail/{no}`. PSM: farklı URL (ör. `/Requests/PsmDetail/{no}`) + PSM'e özel arayüz (sunucu künyesi).

### [BEKLİYOR] Yetki yardımcıları base'e taşıma (~50x tekrar)
- En büyük teknik borç; RİSKLİ (12 dosya, login-as edge). Kendi odaklı turunda + kapsamlı doğrulamayla yapılmalı.

### Kalan düşük/perf — ✅ KÜÇÜK KOVA KAPANDI
- ✅ (v1.18.2) global arama. ✅ (v1.18.4) ACL fail-open→rol-varsayılanı; Project AsSplitQuery; TaskItem AsNoTracking; Reports bounds.
- ✅ (v1.18.5) AsNoTracking sweep tamam (Employee/Team/Category/ActivityLog okuma); ActivityTypeLabels → tek Core helper (Entities.ActivityTypeLabels); ölü kod kaldırıldı (kullanılmayan IsAdmin x2, WireItem StatusLabel/UpdatedAt/ClosedAt).
- [BEKLİYOR — ORTA/migration] RoutineTask/ParentTaskId ölü şema (migration ile temizle ya da RoutineTask özelliğini tamamla).

### [BEKLİYOR] Eksik özellikler (Batch E)
- PDF raporlama; Gantt bağımlılık/kritik yol; toplu (bulk) işlemler; bildirim tercihleri; kişi kartı efor trendi.

---
## (özgün kayıt) — Yeni istekler (2026-07-29)

## Yeni istekler (2026-07-29, öncelikli)

### Y1 — Talebe resim + iç not eklerken "portal yorumu reddetti" hatası
- **Belirti:** Bir talebe **resim koyup iç not** eklendiğinde "Portal yorumu reddetti" hatası alınıyor.
- **Kök neden:** Destek `WriteBackEnabled` açık; iç not da portala POST ediliyor. Resim eklenince istek **multipart** oluyor; **destek'in `/yorumlar` ucu multipart'ı reddediyor** (bilinen destek bug'ı — `destek-entegrasyon-v3-hata-bildirimi.md`). Yani resimli (multipart) iç not, destek düzeltene kadar başarısız + yerelde de kaydedilmiyor.
- **Yapılacak (bizim taraf, destek beklemeden):** İç not'ta (isInternal=true) resim/dosya varsa ve destek multipart reddederse iç notu **yerelde sakla** (portala gönderim opsiyonel/başarısızlığa dayanıklı olsun); ya da iç notların dosyalarını multipart yerine yerelde tut. Dış not için destek düzeltmesi şart. Karar netleştirilip uygulanacak.

### Y2 — Faaliyetler (Activities) görünürlük + arama
- Uzman: yalnız **kendi faaliyetlerini + kendisine atanan faaliyet başlıklarını** görsün.
- **Admin / TakımLideri: hepsini** görsün.
- **Arama** eklensin (başlık vb. ile hızlı bulma) — server-side.
- (Not: talep tarafındaki `ApplyVisibilityScope`/Query desenine benzer kurulacak; `ActivitySubjectAppService`.)

### Y3 — "Çözüldü" ≠ "Kapatılan Talep"
- Şu an arşiv (Kapatılan Talepler) = (Kapandı **veya Çözüldü**) && eforu var (B7).
- **İstenen:** Çözüldü talep **kapatılan sayılmasın**; yalnız **Kapatıldı/Kapandı** yapılınca "Kapatılan Talepler"e düşsün. Çözüldü aktifte/açık kalsın.
- Dosyalar: `ServiceRequestAppService.GetIndexAsync` (Archived tanımı), ilgili sayaç/sekme mantığı.

### Y4 — Denetim Orta + Düşük maddeleri (aşağıdaki liste) uygulanacak.

### Y5 — Destek vs PSM talep DETAY sayfaları AYRI arayüz
- Destek talepleri: `/Requests/Detail/{no}` (mevcut, dış destek arayüzü).
- PSM talepleri: FARKLI URL (ör. `/Requests/PsmDetail/{no}`) + FARKLI arayüz (sunucu kurulum künyesi: requested/installed/services, hostname/IP/OS vb. PSM'e özgü alanlar).
- URL'ye karar serbest; şart: ayrı arayüz. PSM ayrı-tasarım kararıyla (talepler farklı format) uyumlu.

---

## Denetimden (2026-07-29) — ORTA
- **EmployeeAppService** `Delete`/`Update`: TakımLideri için takım-kapsamı yok → başka takımın/Admin'in personelini silebilir/düzenleyebilir. Kapsam kontrolü ekle (Admin/Manager hariç).
- **Detay senkronu watermark sırası:** watermark detay döngüsünden ÖNCE ilerliyor → transient hatada yorum/dosya kalıcı kaybı. Watermark'ı detay sonrası ilerlet veya son-N-gün yeniden dene. (`RequestSyncHostedService`)
- **PortalStatusText normalize:** ingest'te kod/etiket tutarsız saklanıyor → durum dropdown flip-flop. Katalog etiketine normalize et.
- **Yorum write-back çift kayıt:** POST cevabı id vermezse `ExternalCommentId=null` → sonraki senkron aynı yorumu tekrar ekler. cid null ise yerel ekleme veya (body+tarih) dedup.
- **Yetki yardımcıları tekrarı (en büyük borç):** `CurrentContext`/`IsManager`/`SeesAllTeams`/`EffectiveRole`/`CurrentEmployeeTeamId` vb. 12 dosyada ~50x kopya → `ActivityManagementAppServiceBase`'e (veya `ICurrentUserContext`) taşı.
- **`AsNoTracking` tutarsız:** Reports/TaskItem/ActivityLog/Project/Employee/Team okuma uçlarına ekle.
- **GetTeamReport N+1** (`ReportAppService`) → tek sorgu + GroupBy. **ProjectAppService.GetAllAsync** koleksiyon Include + paging kartezyen → projeksiyon/AsSplitQuery.
- **Application'a portal HTTP sızıntısı** (~350 satır HttpClient + auth-header 4-5x tekrar) → `IPortalClient` altyapı servisine çıkar.
- **Controller null-safety/try-catch:** `TasksController.Edit` (GET null), `Delete` (try/catch yok) → Requests desenine hizala. `TaskItemAppService.AddCommentAsync` senkron `.Get` → async.

## Denetimden (2026-07-29) — DÜŞÜK
- Güvenlik başlıkları: CSP + `X-Frame-Options` (nosniff eklendi). Webhook/admin login sabit-zamanlı karşılaştırma + rate-limit/lockout. Admin fallback parola `Admin123!` kaldır. Sayfa ACL fail-open (`catch{}` → CanAccessPage true). AccessControl okuma metotları yetkisiz (rol yapısı sızar).
- Ölü kod: `RoutineTask` (şema var, mantık yok — tamamla ya da kaldır), `ParentTaskId`, kullanılmayan `WireItem` alanları, çift `HoursSpent` kontrolü, kullanılmayan `IsAdmin`.
- `ActivityTypeLabels` 3 yerde tekrar + Reports'ta sınır kontrolü yok.

## Eksik özellik / geliştirme
- **PDF raporlama** (Faz 4; yalnız Excel var). **Gantt bağımlılıkları/kritik yol** (ParentTaskId'yi canlandır). **Global arama** (üst bar). **Toplu (bulk) işlemler**. **Bildirim tercihleri**. Kişi kartı efor trendi.
- **CLAUDE.md "Bekleyen Fazlar"ı güncelle** (Faz 3 tamam, Faz 4 büyük oranda bitmiş).

## Entegrasyon (bekleyen)
- **Destek V3 multipart bug:** bizim istemci hazır; destek ucu multipart'ı reddediyor (Y1 ile ilişkili). `destek-entegrasyon-v3-hata-bildirimi.md` iletilecek.
- **PSM ayrı tasarım** (farklı format). Write-back durum POST kod/etiket teyidi (ilk canlı yazımda).
