# ActivityManagement — Ana Kurallar (Claude Code)

> **⏭️ SIRADAKI OTURUM (2026-07-30, canlı v1.22.4):** Bekleyen işler `yapilacaklar-backlog.md` + detay `history/2026-07-30.md` (+ önceki gün `history/2026-07-29.md`). "devam et" denince ÖNCE bunları oku. **TÜM BACKLOG BİTTİ — bilinen bekleyen iş YOK.** Bu oturumda (v1.21–1.22): Atanmamış (PSM) talepler + destek kişi filtresi; sonra uzun vadeli backlog'un TAMAMI → kişi kartı efor trendi, **PDF raporlama** (PDFsharp/MigraDoc — MIT, Windows Arial font resolver), taleplerde **toplu atama** (bulk), **bildirim tercihleri** (NotificationPreference: e-posta + tip susturma), **Gantt bağımlılık + kritik yol** (TaskDependency + CPM), **yetki yardımcıları base-refactor** (ServiceRequest/TaskItem/ActivitySubject → ActivityManagementAppServiceBase; Family B korundu; login-as canlı doğrulandı). _Önce (v1.17–1.21):_ talep entegrasyonu V2/V3, 6 kritik güvenlik, denetim orta/düşük, Y1–Y5, global arama, izin/görev-atama, ölü şema temizliği, TASARIM BOŞLUĞU EPIC'İ H1–H7 (devir/handover, D:\Uploads, eşzamanlılık, yıllık denetim arşivi, uyarı/eskalasyon, iş yükü, auth denetimi).
>
> ⚠️ **Sürüm şeması:** `1.MAJOR.MINOR`, MINOR .1'den başlar, **".0" KULLANILMAZ** (bkz. ActivityManagementConsts yorumu). 5'ten sonra MAJOR atlar, MINOR .1'e döner.

## Çalışma Geçmişi (ÖNEMLİ — devam etme mekanizması)
- Tüm oturum/çalışma geçmişi `history/` klasöründe **gün gün** tutulur: `history/YYYY-MM-DD.md` (örn. `history/2026-07-22.md`). ISO tarih adı sıralanabilir olduğundan "en güncel dosya" nettir.
- Kullanıcı **"devam et"** dediğinde: ÖNCE `history/` altındaki **en güncel** dosyayı (gerekirse önceki günleri de) oku, kaldığın yerden sürdür.
- Her anlamlı iş/faz bittiğinde ilgili günün dosyasını güncelle: ne yapıldı, hangi dosyalar değişti, deploy/DB durumu, bekleyen işler. Yeni güne geçildiğinde yeni `history/YYYY-MM-DD.md` aç.
- Güncel durum ve bekleyen fazlar için en güncel history dosyasına bak.

## Ortam
- Kaynak: `C:\ActivityManagement` — Canlı: `C:\inetpub\ActivityManagement` (IIS in-process, app pool `ActivityManagement`, port 8090, .NET 8 / ABP MVC).
- DB: **AlwaysOn AG listener `tdvsqlagl1tr1`** (AG: `tdvsqlag1tr1`; nodlar TDVSQL1TR1=primary, TDVSQL2TR1 senkron, TDVSQL3TR1 kullanılmıyor), veritabanı `ActivityManagement`, SQL login `infra`. `appsettings.Production.json`'da DPAPI (LocalMachine) şifreli; `MultiSubnetFailover=True`. (Eski `192.168.31.31` DB'si 2026-07-23'te terk edildi; veri taşınmadı — temiz seed ile başlandı.)
- Read-only DB sorgu yardımcısı: `scratchpad\dbq.ps1` (bağlantı dizesi/şifre ASLA ekrana/dosyaya yazılmaz).

## Denetim (Audit) Logları
- DB tablosu `SystemAuditLogs` (Admin → Sistem Logları ekranı sorgular) **VE** dosya: `<site>/logs/audit/YYYY-MM-DD.log` — gün gün, 5 MB'yi aşınca `YYYY-MM-DD-1.log`, `-2.log` şeklinde bölünür. Her satır `yyyy-MM-dd HH:mm:ss` ile başlar (arama kolaylığı).
- DbContext SaveChanges interceptor tüm Create/Update/Delete'i otomatik yazar (`AuditFileLogger` + `AuditUserContext`).

## IIS Yazma İzinleri (ÖNEMLİ — fresh setup/deploy'da gerekli)
- App pool kimliği `ApplicationPoolIdentity` (IIS_IUSRS grubu). Site kökü varsayılan salt-okunur.
- Uygulamanın yazdığı klasörlere **IIS_IUSRS Modify** verilmeli:
  - `C:\inetpub\ActivityManagement\logs` (audit + hata dosya logları)
  - **`D:\Uploads`** (personel foto / görev yorumu eki / marka logosu — ayrı storage; `appsettings.Production.json` → `Storage:UploadsPath`. Erişilemezse otomatik `wwwroot\uploads`'a düşer. `/uploads` URL'i ayrı `PhysicalFileProvider` ile bu köke bağlı; H2, v1.20.0).
  - Komut: `icacls "<klasör>" /grant "IIS_IUSRS:(OI)(CI)M" /T`

## Deploy (otomatik yapılabilir — bypass permissions modunda)
1. `dotnet publish -c Release -o <staging>` 
2. `app_offline.htm` bırak (in-process app durur, DLL kilidi açılır) → 3 sn bekle
3. Değişen `ActivityManagement.*.dll` (+ `.pdb`) dosyalarını `C:\inetpub\ActivityManagement`'e kopyala
4. `app_offline.htm` sil → `http://localhost:8090/` HTTP 200 doğrula
- Migration'lar açılışta `Database.Migrate()` ile OTOMATİK uygulanır (Startup.cs).
- Yedek: kopyalamadan önce mevcut DLL'ler `scratchpad\backup2`'ye alınır.

## Yazılım Kuralları
- Detaylı gereksinim & mimari: `22072026-gelistirme.md` (Bölüm 6) ve `PROJE_GEREKSINIMLERI.md` (Bölüm 5).
- Clean Architecture / ABP: Domain (Core) → Application (AppService/DTO/AutoMapper) → EntityFrameworkCore → Web (lean controller/MVC). Katman ihlali yok.
- `async/await` zorunlu; read-only sorgularda `.AsNoTracking()`; paging/filtre server-side (`PagedAndSortedResultRequestDto`).
- **Yetki (TÜM AppService'ler):** `[AbpAuthorize]` KULLANILMAZ (AbpPermissionGrants tablosu yok). Yetki `IHttpContextAccessor` üzerinden cookie claim (`ClaimTypes.Role`=`AppRole`, `EmployeeId`) ile **manuel** kontrol edilir. Giriş zorunluluğu global `[Authorize]` filtresiyle sağlanır. Örnek: `TaskItemAppService`, `ActivitySubjectAppService`, `ActivityLogAppService`, `EmployeeAppService`, `ProjectAppService`. Roller: Admin, TakımLideri, Uzman.
- **Görünürlük vs. İşlem kuralı (ZORUNLU):** Görev/faaliyet **görünürlüğü takım bazlı**: Admin tümünü; TakımLideri + Uzman **kendi takımının** görev/faaliyetlerini görür (uzman, takımdaki kişilere atananları da görür). **İşlem** (durum güncelleme, düzenleme, efor girme/silme) ise **"kendine ait olana"** sınırlıdır (`CanEdit`/`CanManage`/`CanLogEffort`); yönetici kendi kapsamında işlem yapar. Proje detayını herkes görebilir (`GetByProjectAsync` kişisel/takım kapsamına takılmaz).
- **Faaliyet (ActivitySubject):** 3 rol de onaysız açar; Uzman'ınki kendine atanır. **Projesiz olabilir ama kategorisiz olamaz** (proje seçilirse kategori projeden dolar). Efor `ActivityLog`'a yazılır ve konu projeye bağlıysa `ProjectId` de set edilir (raporlama). Efor girişinin tek kapısı `ActivitySubjectAppService.LogEffortAsync`; eski `ActivityLogAppService.Create/Delete` yalnız yöneticiye açık.
- **Tarih formatı:** `dd.MM.yyyy` (Startup'ta global `tr-TR` culture; ondalık girişleri `InvariantDecimalModelBinder` ile korunur).
- Yetkisiz erişimde `/Account/Denied`'e atma: base controller `AccessDeniedRedirect()` (toast + güvenli yönlendirme / AJAX'ta 403). Session ölünce cookie middleware Login'e yönlendirir.
- **HATA YÖNETİMI (ZORUNLU — sayfa asla patlamamalı):**
  - Global: `app.UseExceptionHandler("/Home/Error")` — tüm yakalanmayan istisnalar dostça hata sayfasına gider ve `ErrorLog` ile `logs/error/YYYY-MM-DD.log`'a yazılır — gün gün, 5 MB'de `-1/-2` parçalı, satır başında tarih-saat (`UseDeveloperExceptionPage` KULLANMA).
  - Controller action'larında dış veriye/parametreye bağlı işleri `try/catch` ile sar: beklenen durumlar `UserFriendlyException` → `TempData["Uyari"]` + güvenli redirect; beklenmeyenler → `ActivityManagement.Logging.ErrorLog.Write(ex, context)` + dostça mesaj/redirect.
  - `GetAsync`/tekil kayıt dönüşlerinde **null kontrolü** yap (kayıt yoksa "bulunamadı" + redirect; view'a null model gönderme).
  - View'da model alanlarına güvenli eriş (`?.`, `?? "-"`), dizi/enum index'lerinde sınır kontrolü yap.
  - AJAX/API hatalarında uygun HTTP kodu (400/403/500) + JSON mesaj döndür; istemci `abp.notify` ile göstersin.

## Veri Modeli Notları
- Görev = ana kategori + alt kategori + atanan kişi (üst/alt görev kavramı YOK; `ParentTaskId` kolonu DB'de duruyor ama kullanılmıyor).
- `Project`/`TaskItem`/`Employee`/`ActivitySubject` üzerinde `TeamId` (çoklu takım izolasyonuna hazırlık).
- 13 sabit ana kategori (migration seed), alt kategoriler Admin/TakımLideri tarafından yönetilir.

## Bekleyen Fazlar / Backlog
- **Faz 3 — TAMAMLANDI:** Görev onay (`ApprovalStatus`) + yorumda Rich Text (Quill) + Ctrl+V ekran görüntüsü + dosya eki (görev + talep). 
- **Faz 4 — büyük oranda TAMAM:** Audit Log modülü ✓, Kişi Kartı 360° ✓, Gantt (frappe-gantt) ✓ ama bağımlılık/kritik yol YOK, Raporlama Excel ✓ **PDF YOK**.
- **Güncel bekleyenler:** bkz. `yapilacaklar-backlog.md` (PDF rapor, Gantt bağımlılık, global arama, toplu işlem, bildirim tercihleri; yetki-yardımcısı base-refactor; ACL fail-closed; RoutineTask/ParentTaskId ölü şema temizliği).
