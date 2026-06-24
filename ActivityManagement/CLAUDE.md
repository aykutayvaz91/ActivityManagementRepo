# ActivityManagement — Claude Code Oturum Kuralları

Her oturumda bu dosya **ilk okunacak** dosyadır. Aşağıdaki kurallar tartışmasız geçerlidir.

---

## 1. Proje Özeti

**ActivityManagement** — TDV bünyesindeki IT ekibinin görev, faaliyet ve sorumluluk takibini yürüttüğü ASP.NET Zero tabanlı web uygulaması.

| Katman | Proje | Sorumluluk |
|---|---|---|
| Domain | `ActivityManagement.Core` | Entity'ler, enum'lar, authorization config |
| Uygulama | `ActivityManagement.Application` | AppService'ler, DTO'lar, AutoMapper profilleri |
| Veri Erişimi | `ActivityManagement.EntityFrameworkCore` | DbContext, Migration, Seed |
| Sunum | `ActivityManagement.Web.Mvc` | Controller, Razor View, wwwroot |

---

## 2. Teknoloji Yığını (Sabit Sürümler)

| Teknoloji | Sürüm | Not |
|---|---|---|
| .NET | 8.0 | Tüm projelerde hedef framework |
| ASP.NET Zero / ABP | 8.4.0 | `Abp.ZeroCore`, `Abp.AspNetCore` |
| EF Core | 8.0.0 | SQL Server provider |
| Bootstrap | **5.3.3** | CDN via `_Layout.cshtml` — npm/npm yok |
| jQuery | **3.7.x** | CDN — `$()` kullanımı geçerli |
| Font Awesome | 6.5.2 | CDN — ikon sınıfları `fa-*` |
| FullCalendar | 6.1.11 | Sadece takvim sayfalarında |
| Serilog | 8.0.0 | Loglama — `ILogger<T>` inject et |
| AutoMapper | ABP entegreli | `[AutoMapFrom]` / `[AutoMapTo]` attribute |

> **Sürüm değişikliği yasak.** `.csproj` versiyonu onaysız değiştirilmez.

---

## 3. Mimari Kurallar

### 3.1 Katman Bağımlılıkları
```
Web.Mvc  →  Application  →  Core
              ↓
       EntityFrameworkCore  →  Core
```
- `Web.Mvc` doğrudan EF Core veya DbContext kullanamaz.
- `Core` katmanı hiçbir şeye bağımlı olamaz (saf domain).
- AppService'ler arası doğrudan çağrı yerine repository kullanılır.

### 3.2 Namespace Kuralları
- Entity'ler: `ActivityManagement.Entities`
- DTO'lar: `ActivityManagement.<Modül>.Dto`
- AppService'ler: `ActivityManagement.<Modül>`
- Controller'lar: `ActivityManagement.Web.Controllers`
- Sayfalar: `Views/<KontrolörAdı>/<Eylem>.cshtml`

---

## 4. Entity Kuralları (Core Katmanı)

### 4.1 Base Sınıflar
```csharp
// Silme + audit istiyorsan:
public class MyEntity : FullAuditedEntity<long>, IMustHaveTenant { }

// Sadece audit (soft-delete yok):
public class MyEntity : AuditedEntity<long>, IMustHaveTenant { }
```

### 4.2 Zorunlu Kurallar
- **Soft-Delete:** `_repository.DeleteAsync()` veya `IsDeleted = true` — `DbContext.Remove()` **yasak**
- **TenantId:** Her uygulama entity'sinde `public int TenantId { get; set; }` ve `IMustHaveTenant` olmalı
- **Primary Key:** `long` — `int` veya `Guid` kullanılmaz (mevcut tablolarla uyum için)
- **Navigation Property:** `virtual` zorunlu (lazy loading desteği)
- **Collection Init:** `= new List<T>()` ile başlatılır, null bırakılmaz

### 4.3 Enum Kuralları
- Enum değerleri açık sayısal atama alır: `Bakim = 0, Gelistirme = 1, ...`
- Türkçe karaktersiz enum adı (DB'de saklandığı için): `Tamamlandi` (ı → i)
- UI'da Türkçe label için AppService'de string array kullanılır

---

## 5. DTO Kuralları (Application Katmanı)

### 5.1 Standart DTO Setleri
Her modül için üç ayrı DTO:
```
CreateUpdateXxxDto    ← Form girdisi (Create ve Edit paylaşır)
XxxDto                ← Okuma/listeleme çıktısı
GetXxxsInput          ← Filtreleme + sayfalama girdisi
```

### 5.2 Mapping
```csharp
[AutoMapFrom(typeof(MyEntity))]   // Entity → DTO
public class MyDto : FullAuditedEntityDto<long> { }

[AutoMapTo(typeof(MyEntity))]     // DTO → Entity
[AutoMapFrom(typeof(MyDto))]      // DTO ↔ DTO (Edit için)
public class CreateUpdateMyDto { }
```

### 5.3 Hesaplanan Alanlar
- `XxxDto`'ya ek alanlar (örn. `AssignedEmployeeName`) MapToDto() metodunda elle doldurulur
- AutoMapper `Ignore()` yerine `MapToDto()` private metodu tercih edilir
- `CanEdit`, `CanDelete` gibi yetki alanları DTO'ya eklenir, view'da kontrol edilir

---

## 6. AppService Kuralları (Application Katmanı)

### 6.1 İmza ve Base Sınıf
```csharp
public class MyAppService : ActivityManagementAppServiceBase, IMyAppService
{
    private readonly IRepository<MyEntity, long> _repo;
    private readonly IHttpContextAccessor _httpContextAccessor;
}
```

### 6.2 Zorunlu Kurallar
- **Async/Await:** Tüm public metodlar `async Task<T>` — sync bloklar yasak
- **SaveChanges:** ABP UoW otomatik commit eder; explicit `SaveChangesAsync()` yalnızca ID gereken durumda
- **CurrentContext():** Kullanıcı bilgisi (rol, e-posta, EmployeeId) cookie claim'lerinden okunur — her sorgu için DB'ye gidilmez:
  ```csharp
  private (string Role, long? EmployeeId) CurrentContext()
  {
      var user = _httpContextAccessor.HttpContext?.User;
      var role = user?.FindFirst(ClaimTypes.Role)?.Value ?? "Uzman";
      var empIdStr = user?.FindFirst("EmployeeId")?.Value;
      long? empId = long.TryParse(empIdStr, out var p) ? p : null;
      return (role, empId);
  }
  ```
- **Yetki Kontrolü:** AppService içinde `UserFriendlyException` fırlat; Controller'da `try/catch` ile yakala, `ModelState.AddModelError` ile view'a ilet
- **N+1 Yasağı:** `foreach` içinde `GetAsync` çağrısı yasak — `Include()` veya projeksiyon kullan

### 6.3 Sorgu Standartları
```csharp
// Listeler için:
var query = _repo.GetAll()
    .Include(t => t.RelatedEntity)
    .WhereIf(!string.IsNullOrWhiteSpace(input.Filter), t => t.Title.Contains(input.Filter))
    .OrderByDescending(t => t.CreationTime)
    .PageBy(input);                        // Skip/Take ZORUNLU — tüm kayıtları çekme
var items = await query.ToListAsync();

// Tek kayıt için (read-only):
var item = await _repo.GetAll()
    .AsNoTracking()                        // Salt okunur sorgularda ZORUNLU
    .Include(...)
    .FirstOrDefaultAsync(x => x.Id == id);
```

---

## 7. Controller Kuralları (Web.Mvc Katmanı)

### 7.1 Base Sınıf
```csharp
public class MyController : ActivityManagementControllerBase
```

### 7.2 Yetki Kontrolü
```csharp
private bool IsManager() => User.IsInRole("Admin") || User.IsInRole("TakımLideri");

private long? CurrentEmployeeId()
{
    var c = User.FindFirst("EmployeeId")?.Value;
    return long.TryParse(c, out var id) ? id : null;
}
```

### 7.3 POST Metodları
```csharp
[HttpPost]
public async Task<IActionResult> Edit(CreateUpdateTaskItemDto input)
{
    if (!ModelState.IsValid)
    {
        ViewBag.SomeList = ...; // ViewBag'i yeniden doldur
        return View(input);
    }
    try
    {
        await _appService.UpdateAsync(input);
        return RedirectToAction("Index");
    }
    catch (UserFriendlyException ex)
    {
        ModelState.AddModelError("", ex.Message);
        ViewBag.SomeList = ...;
        return View(input);
    }
}
```

### 7.4 AJAX Endpoint'leri
- Form POST + antiforgery token: `@Html.AntiForgeryToken()` her formda zorunlu
- ABP API'ye giden AJAX: `$.ajax({ url: '/api/services/app/...' })`
- Durum güncelleme gibi özel endpoint'ler: MVC controller POST (ABP verb convention'ından kaçınmak için)
- DELETE: ABP API endpoint → `type: 'DELETE'`

---

## 8. View / UI Kuralları

### 8.1 UI Dili
- **Tüm label, başlık, buton, placeholder Türkçe olmalıdır**
- İngilizce teknik terim kabul edilir (örn. "Kubernetes", "SIEM"), ama genel UI terimi Türkçe

| İngilizce | Türkçe |
|---|---|
| Status | Durum |
| Priority | Öncelik |
| Assigned To | Atanan Kişi |
| Due Date | Son Tarih |
| Save | Kaydet |
| Cancel | İptal |
| Delete | Sil |
| Edit | Düzenle |
| Create / New | Oluştur / Yeni |

### 8.2 Bootstrap 5.3 Bileşenleri
Kullanılan standart bileşenler:
- Grid: `container-fluid`, `row`, `col-md-*`
- Tablo: `table table-hover align-middle`
- Kart: `card shadow-sm`, `card-header`, `card-body`
- Form: `form-control`, `form-select`, `form-label`
- Buton: `btn btn-primary`, `btn btn-outline-secondary`, `btn btn-sm`
- Badge: `badge bg-success`, `badge bg-danger`, vb.
- Modal: Bootstrap 5 JS API — `new bootstrap.Modal(el).show()`
- Uyarı: `alert alert-danger`, `alert alert-info`
- Progress: `progress` + `progress-bar`

### 8.3 jQuery Kullanımı
```javascript
// Standart AJAX çağrısı (ABP API)
$.ajax({
    url: '/api/services/app/TaskItem/GetAll',
    data: { maxResultCount: 100, filter: '' },
    success: function(res) {
        var items = res.result.items;
    }
});

// ABP bildirim sistemi
abp.notify.success('İşlem başarılı.');
abp.notify.error('Hata oluştu.');

// Antiforgery token (form POST için)
var token = $('input[name="__RequestVerificationToken"]').val();
```

### 8.4 Silme Onayı
`confirm()` **yasak** — Bootstrap 5 Modal kullanılır:
```html
<div class="modal fade" id="deleteModal" tabindex="-1">
  <div class="modal-dialog modal-dialog-centered">
    <div class="modal-content">
      <div class="modal-header bg-danger text-white">
        <h5 class="modal-title">Sil</h5>
        <button type="button" class="btn-close btn-close-white" data-bs-dismiss="modal"></button>
      </div>
      <div class="modal-body" id="deleteModalBody"></div>
      <div class="modal-footer">
        <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">İptal</button>
        <button type="button" class="btn btn-danger" id="confirmDeleteBtn">Evet, Sil</button>
      </div>
    </div>
  </div>
</div>
```

### 8.5 Sayfa Yapısı (Standart Şablon)
```html
@{
    ViewData["Title"] = "Sayfa Başlığı";
    Layout = "~/Views/Shared/_Layout.cshtml";
}

<!-- Başlık + Aksiyon Butonu -->
<div class="d-flex justify-content-between align-items-center mb-4">
    <h2><i class="fas fa-icon text-primary me-2"></i>Başlık</h2>
    <a href="/Modul/Create" class="btn btn-primary">
        <i class="fas fa-plus me-1"></i>Yeni Kayıt
    </a>
</div>

<!-- İçerik -->
<div id="contentContainer">
    <div class="text-center py-5"><div class="spinner-border text-primary"></div></div>
</div>

@section Scripts {
<script>
$(document).ready(function() { loadData(); });
// ...
</script>
}
```

### 8.6 Breadcrumb
Her iç sayfada zorunlu:
```html
<nav aria-label="breadcrumb">
    <ol class="breadcrumb mb-0">
        <li class="breadcrumb-item"><a href="/Modul">Modül</a></li>
        <li class="breadcrumb-item active">Detay</li>
    </ol>
</nav>
```

---

## 9. EF Core ve Migrasyon Kuralları

### 9.1 DbContext Konfigürasyonu
`ActivityManagementDbContext.OnModelCreating()` içinde:
- Her tablo açıkça `b.ToTable("TableName")` ile belirtilir
- String alanlar `HasMaxLength()` alır
- FK ilişkileri açık `HasOne/WithMany/HasForeignKey/OnDelete` ile tanımlanır
- Multiple cascade path hatasından kaçınmak için `OnDelete(DeleteBehavior.NoAction)` kullan

### 9.2 Migration Akışı
```bash
# Sunucuda (192.168.31.85) çalıştırılır — local'de dotnet SDK yok
cd src/ActivityManagement.EntityFrameworkCore
dotnet ef migrations add <MigrationAdi> --startup-project ../ActivityManagement.Web.Mvc/ActivityManagement.Web.Mvc.csproj
dotnet ef database update --startup-project ../ActivityManagement.Web.Mvc/ActivityManagement.Web.Mvc.csproj
```
- Migration adı `PascalCase` ve açıklayıcı: `AddSecondaryEmployee`, `AddTaskGroupName`
- Migration sonrası local repo'ya sync edilir (sunucudan kopyalanır)

### 9.3 Yasaklar
| Yasak | Doğru Alternatif |
|---|---|
| `context.Remove(entity)` | `IsDeleted = true` (soft-delete) |
| `ToListAsync()` önce filtre yok | `.Where(...)` → `.PageBy(input)` → `.ToListAsync()` |
| `foreach` içinde `GetAsync()` | `Include()` veya JOIN projeksiyon |
| SQL Server'a IP:port olmadan bağlantı | `Server=192.168.31.31,1433;...` |

---

## 10. Kimlik Doğrulama ve Yetkilendirme

### 10.1 Roller
| Rol | Yetki |
|---|---|
| `Admin` | Her şey |
| `TakımLideri` | Görev oluşturma, herkesin görevini düzenleme |
| `Uzman` | Kendi alt görevini oluşturma/düzenleme/silme |

### 10.2 Cookie Claim'leri
```
ClaimTypes.Role      → "Admin" | "TakımLideri" | "Uzman"
ClaimTypes.Email     → kullanıcı e-postası
"EmployeeId"         → Employee.Id (long, string olarak saklanır)
```

### 10.3 Yetki Kontrol Pattern'i
```csharp
// AppService'de:
private bool IsManager(string role) =>
    role == "Admin" || role == "TakımLideri";

// Controller'da:
private bool IsManager() =>
    User.IsInRole("Admin") || User.IsInRole("TakımLideri");

// Erişim reddi:
return Redirect("/Account/Denied");
```

---

## 11. Sunucu ve Deployment

### 11.1 Ortam Bilgileri
| Bilgi | Değer |
|---|---|
| Web Sunucusu | 192.168.31.85 |
| DB Sunucusu | 192.168.31.31,1433 |
| Deploy Path | `C:\inetpub\ActivityManagement` |
| Kaynak Path | `C:\projem\ActivityManagement` |
| IIS App Pool | `ActivityManagement` |
| Domain | `activitymanagement.tdv.org` |
| Erişim | WinRM PSRemoting (`TEST\aykut.ayvaz`) |

### 11.2 Deploy Adımları
```powershell
# 1. App pool durdur
Stop-WebAppPool -Name "ActivityManagement"
Start-Sleep -Seconds 3

# 2. Publish
cd C:\projem\ActivityManagement\src\ActivityManagement.Web.Mvc
$env:ASPNETCORE_ENVIRONMENT = "Production"
dotnet publish -c Release -o "C:\inetpub\ActivityManagement"

# 3. App pool başlat
Start-WebAppPool -Name "ActivityManagement"
```

### 11.3 Hassas Dosyalar (Git'e ASLA eklenmez)
- `appsettings.Production.json` — DB şifresi, Google secret
- `HANDOFF.md` — tüm credential'lar
- Her ikisi `.gitignore`'da tanımlı

---

## 12. Git Kuralları

### 12.1 Branch
- Ana branch: `main`
- Remote: `https://github.com/aykutayvaz91/ActivityManagementRepo`

### 12.2 Commit Mesajı Formatı
```
<Eylem> <ne yapıldı> — kısa açıklama

Eylem örnekleri: Add, Fix, Update, Remove, Refactor
```
Örnekler:
- `Add task hierarchy and group filtering`
- `Fix Edit authorization for unassigned tasks`
- `Update Index view with collapsible parent cards`

### 12.3 Push Kuralı
- **Kullanıcı açık onayı olmadan `git push` yapılamaz**
- Push öncesi `git status` + `git diff --stat` ile nelerin gideceği gösterilir

---

## 13. Kritik Hatırlatıcılar (Özet Tablo)

| Kural | Detay |
|---|---|
| **Soft-Delete** | `context.Remove()` yasak — ABP soft-delete ya da `IsDeleted = true` |
| **Antiforgery** | Her MVC form POST'unda `@Html.AntiForgeryToken()` zorunlu |
| **Modal Silme** | `confirm()` yasak — Bootstrap 5 modal kullan |
| **N+1 Yasağı** | `foreach` içinde DB sorgusu yasak — `Include()` kullan |
| **AsNoTracking** | Read-only sorgularda zorunlu |
| **Sayfalama** | `ToListAsync()` öncesi `PageBy(input)` — tüm kayıt çekme |
| **Türkçe UI** | Tüm label/başlık/buton metni Türkçe |
| **Bootstrap 5.3** | Bootstrap 4 sınıfları (`data-toggle`, `data-target`) yasak |
| **jQuery CDN** | npm/webpack yok — CDN'den gelen `$` kullanılır |
| **CurrentContext** | Kullanıcı bilgisi claim'den okunur — her istek için DB sorgusu atılmaz |
| **UserFriendlyException** | Yetki/iş hatası → AppService'de fırlat, Controller'da yakala |
| **IMustHaveTenant** | Her entity `TenantId` taşır ve interface implement eder |
| **Migration Zorunlu** | Model değişikliği = yeni migration — DB elle değiştirilmez |
| **Push Onayı** | `git push` kullanıcı onayı olmadan yapılmaz |
| **Hassas Veri** | Şifre/token/IP kod dosyalarına veya commit'e yazılmaz |
| **ABP Verb Convention** | `Update*` → PUT, `Delete*` → DELETE — AJAX'ta dikkat |

---

## 14. Proje Terminolojisi

| Teknik Terim | Türkçe Karşılığı |
|---|---|
| Parent Task | Üst Görev |
| Sub Task / Child Task | Alt Görev |
| Assigned Employee | Atanan Kişi / Ana Sorumlu |
| Secondary Employee | 2. Sorumlu / Yedek Sorumlu |
| Assigned By | Atayan Kişi |
| Group Name | Görev Grubu (Sistem Birimi / Network Birimi / Ortak) |
| Activity Type | Aktivite Tipi |
| Completion Percentage | Tamamlanma Yüzdesi |
| Routine Task | Rutin Görev |
| Workflow Status | İş Akışı Durumu |

---

*Oluşturulma: 2026-06-24 — ActivityManagement projesi için özelleştirilmiştir.*
*Stack: ASP.NET Zero 8.4.0 / .NET 8 / EF Core 8 / Bootstrap 5.3.3 / jQuery 3.7*
