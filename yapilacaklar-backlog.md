# Yapılacaklar — Backlog

> **DURUM (2026-07-30, v1.22.4 canlı): TÜM BACKLOG BİTTİ.** Uzun vadeli işler de tamamlandı — yetki base-refactor + Batch E (PDF, Gantt bağımlılık/kritik yol, toplu atama, bildirim tercihleri, kişi kartı efor trendi) + Atanmamış (PSM) talepler + destek kişi filtresi. **Bilinen bekleyen iş YOK.** Detay: `history/2026-07-30.md`.

> DURUM (2026-07-29 sonu, v1.21.0 canlı): Y1–Y5 + denetim ORTA/DÜŞÜK + **TASARIM BOŞLUĞU KAPATMA EPIC'İ (H1–H7)** TAMAMLANDI ve canlıda. Detay: `history/2026-07-29.md` "Tasarım boşluğu kapatma epic'i" bölümü. **Bekleyen tasarım boşluğu YOK.** Kalanlar aşağıda **[BEKLİYOR]** (uzun vadeli: yetki-refactor + Batch E özellikleri). "Devam et"te bu dosya + `history/2026-07-29.md` okunmalı.

## ✅ TASARIM BOŞLUĞU KAPATMA (2026-07-29, v1.20.0→1.21.0, canlı)
- **H1** Devir (handover): izin/pasif/silme'de açık görev+talepler yedeğe devredilir + iç not ile "neden" izli.
- **H2** Upload → **D:\Uploads** (ayrı storage; UploadStorage + ayrı static provider + nosniff/attachment).
- **H3** Eşzamanlılık: TaskItem düzenlemede OriginalStamp round-trip → sessiz üzerine yazma önlenir.
- **H4** Yıllık arşiv: SystemAuditLogArchive tablosu + ArchiveHostedService (geçmiş yıl taşınır) + "Denetim Arşivi" sorgu ekranı; eski bildirim purge.
- **H5** Sync art-arda hata → admin uyarısı; SLA İHLALİ → takım liderine eskalasyon.
- **H6** İş yükü: atama dropdown'larında "(N açık iş)".
- **H7** Auth denetimi: giriş/başarısız/çıkış/login-as → dosya log + SystemAuditLogs (uçtan uca doğrulandı).
- (Rutin görev ayrı özelliği İSTENMEDİ — faaliyet başlığı altında efor ile yönetiliyor.)

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

### ✅ Yetki yardımcıları base'e taşıma — TAMAMLANDI (v1.22.4, 2026-07-30)
- ServiceRequest/TaskItem/ActivitySubject'teki birebir tekrar `ActivityManagementAppServiceBase`'e taşındı; base deps 3 ctor'da açıkça atandı. Family B (Project/Category/SubCatResp — "Manager" hariç) korundu (`new`). Canlı doğrulandı (admin-self vs login-as scoping). Detay: `history/2026-07-30.md`.

### Kalan düşük/perf — ✅ KÜÇÜK KOVA KAPANDI
- ✅ (v1.18.2) global arama. ✅ (v1.18.4) ACL fail-open→rol-varsayılanı; Project AsSplitQuery; TaskItem AsNoTracking; Reports bounds.
- ✅ (v1.18.5) AsNoTracking sweep tamam (Employee/Team/Category/ActivityLog okuma); ActivityTypeLabels → tek Core helper (Entities.ActivityTypeLabels); ölü kod kaldırıldı (kullanılmayan IsAdmin x2, WireItem StatusLabel/UpdatedAt/ClosedAt).
- ✅ (v1.19.1) RoutineTask/ParentTaskId ölü şema TEMİZLENDİ — RoutineTask entity/tablo/DbSet/config, TaskItem.ParentTaskId/ParentTask/SubTasks/IsRoutine/RoutineTaskId/RoutineTask, Employee.RoutineTasks nav, RoutineTasks permission tanımları + DTO alanları kaldırıldı; migration (veri yoktu). Tasks sayfaları 200.

### ✅ Eksik özellikler (Batch E) — TAMAMLANDI (v1.22.0→1.22.4, 2026-07-30)
- ✅ PDF raporlama (PDFsharp/MigraDoc, Türkçe font); ✅ Gantt bağımlılık/kritik yol (TaskDependency + CPM); ✅ toplu (bulk) atama (talepler); ✅ bildirim tercihleri (NotificationPreference); ✅ kişi kartı efor trendi (son 12 ay). Detay: `history/2026-07-30.md`. **Bekleyen backlog YOK.**

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

## Yeni istek (2026-07-29) — İzin & görev atama — ✅ (v1.19.2)
- İzinli personele görev atamada "uyar ama izin ver": atama dropdown'ında izinli kişi "(izinli)" işaretli; ana sorumlu izinliyse atamadan önce onay istenir; otomatik yedeğe-swap KALDIRILDI (kişi bilinçli seçiliyor + uyarı notu). IsOnLeaveNow tarih-aralığı duyarlı.
- İzin girişi: Personel → Düzenle'de "İzinli" + **İzin Başlangıç** (yeni) + İzin Bitiş; kişi kartında izinli/izin-planlı rozeti. LeaveStartDate migration eklendi.
