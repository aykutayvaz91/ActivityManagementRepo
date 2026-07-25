# ActivityManagement — Sistem Dokümanı (Nasıl Çalışır)

> Faaliyet / Görev / Talep yönetim sistemi. Bu doküman sistemin **nasıl çalıştığını**, ana kavramları, modülleri, güvenlik ve deploy mimarisini özetler. Geliştirme kuralları için `CLAUDE.md`, günlük çalışma geçmişi için `history/` klasörüne bakın.

---

## 1. Genel Bakış

Altyapı ekibinin tüm işini tek yerden yönetmesi ve **efor (harcanan süre)** takibi yapması için kurulmuş bir web uygulaması. Üç tür "iş" vardır ve hepsi ortak bir efor tablosuna yazılır:

| İş türü | Nedir | Kaynağı |
|---|---|---|
| **Görev (TaskItem)** | Kategori + alt kategori + atanan kişi + SLA'lı iş | Elle oluşturulur |
| **Faaliyet Konusu (ActivitySubject)** | SLA'sız, periyodik/rutin iş başlığı | Elle oluşturulur (3 rol de açar) |
| **Talep (ServiceRequest)** | Dış portallardan gelen sunucu kurulum / destek talebi | psm.tdv.org, destek.cmit.com.tr (veya elle) |

Hepsine harcanan efor **`ActivityLog`** tablosuna yazılır → günlük efor, raporlar ve %ilerleme buradan beslenir.

---

## 2. Mimari ve Teknoloji

- **Clean Architecture / ABP Framework (aspnetboilerplate)** — ASP.NET Core MVC, .NET 8, EF Core, SQL Server.
- Katmanlar (bağımlılık yukarı doğru):
  - **Core** (`ActivityManagement.Core`) — Entity'ler, enum'lar, DPAPI, PageCatalog, Logging.
  - **Application** — AppService'ler (iş mantığı), DTO'lar, AutoMapper. Dış dünyaya `/api/services/app/...` olarak açılır.
  - **EntityFrameworkCore** — DbContext, Migration'lar, Seed, Audit interceptor.
  - **Web.Mvc** — Controller (ince) + Razor View + arka plan servisleri (HostedService) + Startup.
- **Sunum:** Sunucu-render Razor + Bootstrap 5 + jQuery + `abp.notify` (toast). SPA yoktur.
- Detaylı gereksinim/mimari: `22072026-gelistirme.md`, `PROJE_GEREKSINIMLERI.md`.

### Ortam
- Kaynak: `C:\ActivityManagement` · Canlı: `C:\inetpub\ActivityManagement` (IIS in-process, app pool `ActivityManagement`, port 8090, alan `activitymanagement.tdv.org`).
- **DB:** AlwaysOn AG listener `tdvsqlagl1tr1`, veritabanı `ActivityManagement`, SQL login `infra`. `appsettings.Production.json`'da bağlantı dizesi **DPAPI (LocalMachine)** şifreli, `MultiSubnetFailover=True`.
- Migration'lar açılışta `Database.Migrate()` ile **otomatik** uygulanır (Startup); ardından `SeedDataBuilder` idempotent seed çalışır.

---

## 3. Kimlik Doğrulama ve Yetkilendirme

### Giriş
- **Google OAuth** (kurumsal) veya **config-admin** (`appsettings`'te tanımlı, DPAPI şifreli parola). Giriş zorunluluğu global `[Authorize]` filtresiyle sağlanır (`/Account/Login` anonim).
- Cookie'de tutulan claim'ler: `ClaimTypes.Role` (= `AppRole`), `EmployeeId`, `AdminOwnEmployeeId`, `ActingAsName`.

### Roller
`Admin`, `TakımLideri`, `Uzman`. Yetki `[AbpAuthorize]` ile **DEĞİL**, AppService içinde `IHttpContextAccessor` üzerinden claim okunarak **manuel** kontrol edilir (AbpPermissionGrants tablosu yoktur).

### "Login-as" (kişi olarak işlem yap) ve admin-self
- Config-admin (Sistem Yöneticisi), **başka bir kişi kimliğine geçebilir** (`/Admin/ActAs`): cookie yeniden imzalanır (`EmployeeId` = seçilen kişi, `AdminOwnEmployeeId` = admin'in kendi personel id'si sabit).
- **admin-self** kavramı (tüm kapsam servislerinde ortak): `isAdmin && (!empId || !ownId || empId==ownId)`
  - Config-admin kendi kimliğinde → tümünü görür.
  - `AdminOwnEmployeeId` claim'i olmayan Google-admin → tümünü görür.
  - Config-admin **login-as başkası** (empId≠ownId) → o kişinin **takımına** kapsamlanır.
  - Normal kullanıcı → her zaman kendi takımına kapsamlanır.

### Sayfa erişim matrisi (Rol × Sayfa)
- `PageCatalog` sayfa anahtarlarını tanımlar (Dashboard, Employees, Projects, Work, Requests, Tasks, Board, MyTasks, Activities, DailyEffort, TaskQuery, Reports, Admin).
- `RolePageAccess` tablosu + `AccessControlAppService` hangi rolün hangi sayfayı gördüğünü tutar; Admin panelinden yönetilir. Menüler ve controller `EnsurePageAccess(...)` bu matrise bakar. Admin her sayfaya erişir (kilitlenme önlemi).

### Dynamic API güvenliği (ÖNEMLİ)
ABP, tüm AppService public metotlarını `/api/services/app/{Servis}/{Metot}` olarak açar — controller'daki kontroller API'den **baypas edilebilir**. Bu yüzden **yetki her zaman AppService içinde** yapılır:
- Rol atama (`Employee.UpdateRole`), iş akışı durumları (`WorkflowStatus.*`), tema/e-posta/entegrasyon ayarları, kategori/takım yönetimi → **`EnsureAdmin`**.
- Personel oluşturma: Admin olmayan (TakımLideri) daima **"Uzman"** oluşturur; rol/aktiflik/hesap/takım gibi hassas alanlar yalnız Admin'e açık (**yetki yükseltme kapalı**).
- Efor düzenle/sil: TakımLideri yalnız **kendi takımı**.
- Portal upsert / inbound anahtar okuma: kimliği doğrulanmış non-admin çağrıya **guard** (yalnız anonim webhook + HostedService + Admin).

---

## 4. Ana Kavramlar ve Veri Modeli

- **Employee** — personel (Ad, Soyad, Title, `AppRole`, `Email`, `TeamId`, `IsOnLeave`/`LeaveEndDate`). Config-admin için otomatik "Sistem Yöneticisi" kaydı (hiçbir takıma bağlanmaz).
- **Team** — takım (lider = `LeaderId`). Varsayılan: "Infrastructure Team".
- **Project** — proje (kod PRJ-###, 1./2. sorumlu, kategori, takım, durum).
- **Category / SubCategory** — 13 sabit ana kategori (seed) + alt kategoriler (Admin/Lider yönetir).
- **TaskItem** — görev (kategori/alt-kategori, atanan + 2. sorumlu, `StartDate`/`DueDate` (zorunlu), `Status` (TaskStatus), `PriorityScore` 1-10, `ApprovalStatus`, `ActualHours`, `CompletionPercentage`, yorum/ek).
- **ActivitySubject** — faaliyet konusu (kategori zorunlu, proje opsiyonel, uzmana atanır).
- **ServiceRequest** — talep (`Source`, `ExternalRef`/`ExternalUrl`, talep eden, `Status` (RequestStatus), `PriorityScore`, atanan, takım, `ReceivedDate`/`DueDate`/`ResolvedDate`/`ClosedDate`, `ExtraInfo`).
- **ActivityLog** — **birleşik efor tablosu**: `EmployeeId` + (`TaskItemId` | `ProjectId` | `ActivitySubjectId` | `ServiceRequestId`) + tarih + saat + tip. Tüm efor buraya yazılır.
- **Notification** — kişiye özel in-app bildirim (okundu/okunmadı).
- **IntegrationSettings / IntegrationSource** — entegrasyon ayarları (webhook anahtarı + pull kaynak config; anahtarlar DPAPI şifreli).
- **WorkflowStatus** — Kanban/Pano kolonları (TaskStatus ile eşleşen 5 durum).
- **SystemAuditLog** — tüm Create/Update/Delete otomatik denetim kaydı.

### Görünürlük vs. İşlem kuralı (ZORUNLU)
- **Görünürlük takım bazlı:** Admin-self tümünü; diğerleri kendi **takımının** görev/faaliyet/taleplerini görür.
- **İşlem "kendine ait olana" sınırlı:** durum güncelleme, düzenleme, efor girme → `CanEdit`/`CanManage`/`CanLogEffort`. Yönetici kendi kapsamında işlem yapar.
- Proje detayı herkese açıktır (kişisel/takım kapsamına takılmaz).

---

## 5. Modüller ve Ekranlar (Menü)

```
İşlerim ▾   Genel Bakış · Görevlerim/Görevler · Pano · Faaliyetler · Görev Sorgula
Talepler ▾  Tüm Talepler · Sunucu Talepleri · Destek Talepleri
Günlük Efor · Projeler · Personeller · Raporlar ▾ · Admin
🔔 (bildirim zili)   👤 (kullanıcı / login-as)
```

- **İşlerim → Genel Bakış (`/Work`):** kişinin açık **Görev + Talep + Faaliyet**'i tek listede, gecikmiş→yakın SLA→yüksek önem sırasıyla + özet kartlar. (Sistem Yöneticisi'nde personel yoksa bilgilendirme.)
- **Görevlerim / Görevler:** Sistem Yöneticisi kendi kimliğinde "Görevler" (kategoriler); login-as/normal kullanıcı "Görevlerim".
- **Pano (`/Tasks/Board`):** Kanban. Mod seçici (İkisi/Görevler/Talepler); görev + talep kartları sürükle-bırakla durum günceller (yalnız kendine ait kartlar).
- **Talepler (`/Requests`):** Sunucu/Destek sekmeleri; Ata / Durum / Efor / Detay. Yönetici "Yeni Talep" açar.
- **Günlük Efor (`/Activities/Today`):** seçili günün eforu, 8 saate tamamla, serbest efor ekleme — hedef: **Görev / Talep / Faaliyet / Proje**.
- **Projeler:** "Projelerim" (1. sekme) + "Tüm Projeler". **Personeller.** (Her ikisi takım-kapsamlı.)
- **Raporlar:** Kişisel + Ekip (efor, günlük kırılım, proje/tip bazlı, Excel). Talep eforu "Talep" tipiyle dahildir.
- **Admin Panel:** Rol erişim matrisi, roller, kategoriler, durumlar, takımlar, tema, e-posta/SMTP, **entegrasyon**, faaliyet tipleri, sistem logları, login-as.

---

## 6. Efor ve İlerleme % Mantığı

- **Efor:** tek kapı — ilgili işin atanan kişisi kendi adına girer (`ActivitySubject.LogEffort`, `ServiceRequest.LogEffort`, Günlük Efor). Efor `ActivityLog`'a yazılır; iş bir projeye bağlıysa `ProjectId` de set edilir (raporlama).
- **Görev `ActualHours`** = o göreve ait `ActivityLog` toplamı (efor eklenince/silinince/değişince otomatik senkron).
- **İlerleme % (duruma göre)** — görev ve talepte uyumlu:
  - Tamamlandı/Çözüldü/Kapandı → **%100**
  - Devam Ediyor → taban **%25** (kullanıcı 30/40… yapabilir, korunur)
  - Beklemede/Yeni/Atandı → **%0**; Ertelendi/İptal → mevcut korunur.

---

## 7. Bildirimler (In-app, Polling)

- **Zil (🔔) + okunmamış rozeti** üst menüde; açılır listede son bildirimler.
- **~25 sn polling** (`/Notifications/Summary`) → yeni bildirim gelince **yandan toast + Web Audio "ding"** (ses tarayıcı autoplay kuralı gereği ilk kullanıcı etkileşiminden sonra açılır).
- **Tetikleyiciler:** Görev atandı (oluştur/yeniden ata), Talep atandı (ata/oluştur), **SLA yaklaştı** (görev + talep, yarın SLA'sı olanlara; mevcut e-postaya ek).
- Kural: **kendi işlemine bildirim gitmez**; bildirim yalnız kendi `EmployeeId`'ne (login-as uyumlu).
- Oluşturma `NotificationManager` ile yapılır (API'ye açık değildir — spam engeli).

---

## 8. Portal Entegrasyonu (Faz 2)

İki portaldan gelen talepler (psm.tdv.org = Sunucu Kurulum, destek.cmit.com.tr = Dış Destek) sisteme aktarılır. **Admin panelinden yönetilir (`/Admin/Integration`), varsayılan kapalıdır.**

- **Gelen (Webhook, PUSH):** portallar `POST /api/integration/requests`'e `X-Api-Key` header'ıyla talep gönderir. Anahtar admin panelinden girilir (yoksa 503). `(Source, ExternalRef)` ile **idempotent upsert**.
- **Giden (Pull):** `RequestSyncHostedService` aktif kaynakların okuma API'sini periyodik çeker (JSON), `updatedSince` watermark ile artımlı; map + idempotent upsert. Config (BaseUrl/ApiKey/filtre/aralık) admin panelinden.
- **Alan çözümleme:** `assigneeEmail`→personel, `group`→takım, durum/öncelik metni→enum. Atama/durum yerelde korunur; portal yalnız kapanış/iptal bildirince kapatır. **Parolalar asla çekilmez.**
- Portal ekiplerine iletilecek sözleşme: `destek-entegrasyon.md`, `psm-entegrasyon.md`.

---

## 9. Güvenlik Özeti

- **Sırlar şifreli:** DB'deki SMTP parolası ve entegrasyon anahtarları **DPAPI (LocalMachine)** ile şifrelenir (`DpapiProtector`, "DPAPI:" önekli). Açılışta legacy düz-metin otomatik şifrelenir. `appsettings.Production.json`'daki bağlantı dizesi/parola da DPAPI şifreli (git dışı).
- **Yetki AppService seviyesinde** (dynamic API baypasına karşı) — bkz. §3.
- **Rol yükseltme kapalı** (yalnız Admin rol atar / hassas alan değiştirir).
- **Denetim (Audit):** DbContext SaveChanges interceptor tüm Create/Update/Delete'i `SystemAuditLogs` tablosuna + `logs/audit/YYYY-MM-DD.log` dosyasına yazar.

## 10. Hata Yönetimi ve Loglama

- Global `app.UseExceptionHandler("/Home/Error")` — yakalanmayan istisnalar dostça hata sayfasına gider, `logs/error/YYYY-MM-DD.log`'a yazılır (gün gün, 5 MB'de parçalı).
- Controller action'ları dış veriye bağlı işleri try/catch ile sarar: beklenen durumlar `UserFriendlyException` → toast + güvenli redirect; beklenmeyenler `ErrorLog.Write` + dostça mesaj.
- Yetkisiz erişimde `/Account/Denied` yerine toast + güvenli yönlendirme (AJAX'ta 403).

---

## 11. Deploy ve IIS

**Deploy (bypass permissions modunda otomatik):**
1. `dotnet build -c Release`
2. `app_offline.htm` bırak (in-process app durur, DLL kilidi açılır) → 3 sn bekle
3. Değişen `ActivityManagement.*.dll` (+ `.pdb`) dosyalarını canlıya kopyala
4. `app_offline.htm` sil → `http://localhost:8090/` HTTP 200/302 doğrula
- Migration'lar açılışta otomatik uygulanır; yedek `scratchpad/backup2`'ye alınır.
- Kaynak GitHub'a push edilir (`aykutayvaz91/ActivityManagementRepo`).

**IIS yazma izinleri** (fresh setup): `IIS_IUSRS` için `logs` ve `wwwroot/uploads` klasörlerine Modify:
`icacls "<klasör>" /grant "IIS_IUSRS:(OI)(CI)M" /T`

---

## 12. Kod/Klasör Rehberi

| Konu | Yer |
|---|---|
| Entity'ler | `Core/Entities/` |
| Yetki matrisi anahtarları | `Core/Authorization/PageCatalog.cs` |
| Sır şifreleme | `Core/Security/DpapiProtector.cs` |
| İş mantığı (AppService) | `Application/{Alan}/` |
| Bildirim | `Application/Notifications/` + `Web.Mvc/Controllers/NotificationsController.cs` |
| Entegrasyon | `Application/SystemSettings/Integration*` + `Web.Mvc/Controllers/IntegrationController.cs` + `Web.Mvc/BackgroundJobs/RequestSyncHostedService.cs` |
| DbContext / Migration / Seed | `EntityFrameworkCore/EntityFrameworkCore/` |
| Controller (ince) | `Web.Mvc/Controllers/` |
| Görünümler | `Web.Mvc/Views/` |
| Arka plan servisleri | `Web.Mvc/BackgroundJobs/` (SLA hatırlatma, talep senkron) |
| Menü/layout/bildirim JS | `Web.Mvc/Views/Shared/_Layout.cshtml` |

---

## 13. Bekleyen (Yol Haritası)

- **Bildirim N2:** SignalR anlık push, kullanıcı ses/tip tercihleri, durum-değişti & yorum tetikleyicileri.
- **Faz 3:** Görev onay akışı + Rich Text yorum + Ctrl+V ekran görüntüsü.
- **Faz 4:** Kişi Kartı 360° + MS Project tarzı Gantt + Excel/PDF raporlama.
- **Entegrasyon:** portal ekiplerinden API bilgisi gelince canlı bağlantı (config admin panelinden).

---
*Güncel ayrıntılar için `history/` altındaki en güncel tarih dosyasına bakın.*
