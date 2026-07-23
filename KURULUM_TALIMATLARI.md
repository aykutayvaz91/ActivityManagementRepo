# ActivityManagement - Kurulum Talimatları

## Ön Gereksinimler

- .NET 7 SDK veya üzeri
- SQL Server (LocalDB / Express / Developer)
- Visual Studio 2022 veya VS Code
- Node.js (isteğe bağlı, libman için)

---

## Adım 1: Bağlantı Stringini Ayarlayın

`src/ActivityManagement.Web.Mvc/appsettings.json` dosyasında:

```json
"ConnectionStrings": {
  "Default": "Server=localhost;Database=ActivityManagement;Trusted_Connection=True;TrustServerCertificate=True;"
}
```

**LocalDB kullanıyorsanız:**
```json
"Default": "Server=(localdb)\\mssqllocaldb;Database=ActivityManagement;Trusted_Connection=True;"
```

---

## Adım 2: NuGet Paketlerini Yükleyin

```bash
cd C:\projem\ActivityManagement
dotnet restore
```

---

## Adım 3: Veritabanını Oluşturun (EF Core Migration)

```bash
cd src\ActivityManagement.Web.Mvc

# Migration oluştur (ilk kez)
dotnet ef migrations add InitialCreate `
  --project ..\ActivityManagement.EntityFrameworkCore\ActivityManagement.EntityFrameworkCore.csproj `
  --startup-project ActivityManagement.Web.Mvc.csproj

# Veritabanını güncelle
dotnet ef database update `
  --project ..\ActivityManagement.EntityFrameworkCore\ActivityManagement.EntityFrameworkCore.csproj `
  --startup-project ActivityManagement.Web.Mvc.csproj
```

**Visual Studio Package Manager Console:**
```powershell
# Default project: ActivityManagement.EntityFrameworkCore
Add-Migration InitialCreate
Update-Database
```

---

## Adım 4: Frontend Kütüphanelerini Yükleyin

`src/ActivityManagement.Web.Mvc/wwwroot/lib/` klasörüne aşağıdaki kütüphaneleri indirin:

### Bootstrap 5.3
```
wwwroot/lib/bootstrap/dist/css/bootstrap.min.css
wwwroot/lib/bootstrap/dist/js/bootstrap.bundle.min.js
```

### jQuery 3.7
```
wwwroot/lib/jquery/dist/jquery.min.js
```

### Font Awesome 6 (Free)
```
wwwroot/lib/font-awesome/css/all.min.css
wwwroot/lib/font-awesome/webfonts/
```

### FullCalendar 6
```
wwwroot/lib/fullcalendar/main.min.css
wwwroot/lib/fullcalendar/main.min.js
wwwroot/lib/fullcalendar/locales/tr.js
```

### jQuery Validation
```
wwwroot/lib/jquery-validation/dist/jquery.validate.min.js
wwwroot/lib/jquery-validation-unobtrusive/jquery.validate.unobtrusive.min.js
```

**libman.json ile otomatik indirmek için** projeye `libman.json` ekleyip Visual Studio'dan "Restore Client-Side Libraries" yapabilirsiniz.

---

## Adım 5: Projeyi Çalıştırın

```bash
cd src\ActivityManagement.Web.Mvc
dotnet run
```

Ya da Visual Studio'da `F5` ile başlatın.

Tarayıcıda açılacak adres: `https://localhost:7XXX/`

---

## Seed Data

Proje ilk çalıştırıldığında `SeedDataBuilder` otomatik olarak şunları oluşturur:

| Veri | İçerik |
|------|--------|
| Personeller | Ahmet Yılmaz (Takım Lideri), Ayşe Kaya (Sistem Mühendisi), Mehmet Demir (Senior Geliştirici), Fatma Çelik (DevOps) |
| Projeler | İK Portalı, ERP Entegrasyonu, Mobil Uygulama v2 |
| Görevler | 4 örnek görev (farklı durum ve önceliklerde) |
| Faaliyetler | Son 14 günün çalışma kayıtları |

---

## Proje Yapısı

```
ActivityManagement/
├── ActivityManagement.sln
└── src/
    ├── ActivityManagement.Core/           # Domain katmanı
    │   ├── Entities/                      # Employee, Project, Task...
    │   ├── Authorization/                 # Permissions
    │   └── Localization/                  # tr & en XML
    │
    ├── ActivityManagement.Application/    # Uygulama katmanı
    │   ├── Employees/                     # EmployeeAppService + DTOs
    │   ├── Projects/                      # ProjectAppService + DTOs
    │   ├── Tasks/                         # TaskItemAppService + DTOs
    │   ├── Activities/                    # ActivityLogAppService
    │   └── Reports/                       # ReportAppService
    │
    ├── ActivityManagement.EntityFrameworkCore/  # EF Core
    │   └── EntityFrameworkCore/
    │       ├── ActivityManagementDbContext.cs
    │       ├── Migrations/
    │       └── Seed/SeedDataBuilder.cs
    │
    └── ActivityManagement.Web.Mvc/        # ASP.NET Core MVC
        ├── Controllers/                   # MVC Controllers
        ├── Views/                         # Razor Views
        │   ├── Employees/ (Index, Card, Create, Edit)
        │   ├── Projects/ (Index, Detail, Create, Edit)
        │   ├── Tasks/ (Index, Detail, Create, Edit)
        │   ├── Reports/ (PersonalForm, PersonalReport, TeamReport)
        │   └── Shared/_Layout.cshtml
        ├── wwwroot/
        │   ├── css/site.css
        │   └── js/site.js
        ├── appsettings.json
        ├── Program.cs
        └── Startup.cs
```

---

## URL Haritası

| URL | Açıklama |
|-----|----------|
| `/` | Ana Sayfa - Dashboard |
| `/Employees` | Personel listesi |
| `/Employees/Card/{id}` | Kişi kartı + takvim + faaliyetler |
| `/Employees/Create` | Yeni personel |
| `/Projects` | Proje listesi |
| `/Projects/Detail/{id}` | Proje detayı + ekip + görevler |
| `/Tasks` | Görev listesi |
| `/Tasks/Detail/{id}` | Görev detayı + yorumlar |
| `/Reports/Personal` | Kişisel faaliyet raporu |
| `/Reports/Team` | Ekip raporu |

---

## API Endpoint'leri (ABP Dynamic API)

ABP Framework AppService sınıflarını otomatik API endpoint'e dönüştürür:

| Service | URL |
|---------|-----|
| EmployeeAppService | `/api/services/app/Employee/*` |
| ProjectAppService | `/api/services/app/Project/*` |
| TaskItemAppService | `/api/services/app/TaskItem/*` |
| ActivityLogAppService | `/api/services/app/ActivityLog/*` |
| ReportAppService | `/api/services/app/Report/*` |

---

## Olası Sorunlar

**Migration hatası:** ABP Zero entity'leri (AbpUsers, AbpRoles vb.) DbContext'e otomatik eklenir.
Eğer hata alırsanız `ActivityManagementDbContext`'in `AbpZeroDbContext<Tenant,Role,User,...>`'den türediğinden emin olun.

**LocalDB bağlantı hatası:** SQL Server Object Explorer'dan LocalDB instance'ınızı başlatın.

**Font/ikon görünmüyor:** Font Awesome CDN ile hızlıca başlamak için `_Layout.cshtml`'e CDN linki ekleyebilirsiniz:
```html
<link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.4.0/css/all.min.css" />
```
