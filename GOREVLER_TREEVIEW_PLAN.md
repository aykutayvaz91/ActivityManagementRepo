# Görevler & Görevlerim — Treeview Yeniden Tasarım Planı

**Tarih:** 2026-07-22
**Kapsam:** `ActivityManagement.Web.Mvc` (MVC katmanı, sadece UI + controller yetkilendirme)

## 1. Amaç (Kullanıcı Gereksinimi)

- **Görevler** sayfası (`/Tasks` = `Index`) sadece **kategoriler ve alt kategorileri** içeren bir ağaç (treeview) olacak ve **yalnızca Admin** rolü görüntüleyebilecek.
- **Uzman** (admin olmayan) kullanıcılar aynı ağacı zaten **Görevlerim** (`/Tasks/MyTasks`) altında görebildikleri için Görevler sayfasına ihtiyaçları yok.
- Treeview içindeki **checkbox'lar kaldırılacak** (her iki sayfada da).
- Bir **alt kategori seçilince**, o alt kategoriye ait görevler doğrudan **sağ tarafta filtrelenerek** listelenecek.
- Görevler (admin) sayfasında seçilen alt kategoride **tüm görevler** (her durum: beklemede, devam eden, tamamlanmış, iptal, ertelendi) gösterilecek.

## 2. Karar Notları

| Karar | Cevap |
|-------|-------|
| Değişiklik kapsamı | Her iki sayfa (Görevler + Görevlerim) |
| Admin sağ liste içeriği | Tüm görevler (durum filtresi yok) |
| Treeview kütüphanesi | Harici kütüphane YOK — mevcut hafif custom ağaç (caret ile aç/kapa) sürdürülecek |
| Sağ panel veri kaynağı (Görevler) | `/api/services/app/TaskItem/GetAll?subCategoryId=...` (API `SubCategoryId`/`CategoryId` filtresini zaten destekliyor) |
| Sağ panel veri kaynağı (Görevlerim) | Zaten sunucudan yüklenen kişisel görevler; seçime göre **istemci tarafı** filtre (mevcut davranış korunur, sadece checkbox → tık'a çevrilir) |

### Araştırma Özeti (master-detail treeview deseni)
- Yaygın desen: solda hiyerarşik ağaç, düğüme tıklayınca sağ panele AJAX ile detay yükleme (Telerik/DevExpress/Syncfusion örnekleri bu deseni izliyor).
- Hafif/bağımsız çözümler (Treejs, patternfly-bootstrap-treeview) mevcut; ancak proje zaten kendi minimal ağacını (`toggleTreeNode`) kullanıyor, bağımlılık eklemeye gerek yok.

Kaynaklar:
- https://www.telerik.com/forums/complete-sample-of-treeview-in-left-pane-and-detail-in-right-pane
- http://www.dotnetawesome.com/2014/01/how-to-populate-treeview-nodes-on-demand-using-ajax-aspnet.html
- https://github.com/patternfly/patternfly-bootstrap-treeview
- https://www.cssscript.com/tree-view-checkboxes/

## 3. Yapılacak Değişiklikler (dosya bazında)

### 3.1 `Views/Shared/_Layout.cshtml`
- "Görevler" (`/Tasks`) nav linkini `@if (User?.IsInRole("Admin") == true)` bloğuna al. Böylece admin olmayanlar menüde görmez. "Görevlerim" ve "Tamamlanmış Görevler" linkleri herkeste kalır.

### 3.2 `Controllers/TasksController.cs` — `Index`
- `Index()` → `async Task<IActionResult> Index()` yapılacak.
- Admin değilse `Redirect("/Account/Denied")`.
- `ViewBag.Categories = await _categoryAppService.GetAllAsync(onlyActive: true);` (sol ağaç için).

### 3.3 `Views/Tasks/Index.cshtml` — tam yeniden tasarım
- **Sol sütun (col-lg-4):** Kategori → Alt kategori ağacı.
  - Checkbox YOK.
  - Kategori satırı: caret ile aç/kapa (alt kategorileri gösterir).
  - Alt kategori satırı: tıklanabilir; tıklayınca aktif olarak vurgulanır (`active` sınıfı) ve sağ panel o `subCategoryId` ile yüklenir.
  - Kategori adına tıklama: o kategorinin tüm görevlerini `categoryId` ile yükler (opsiyonel kolaylık).
  - "Temizle" düğmesi seçimi sıfırlar.
- **Sağ sütun (col-lg-8):** Seçilen (alt)kategoriye ait görev listesi.
  - `/api/services/app/TaskItem/GetAll` çağrısı `subCategoryId`/`categoryId` ile (maxResultCount yüksek, durum filtresi yok = tüm görevler).
  - Mevcut parent/child kart render mantığı (`renderParentCard`/`renderChildRow`) korunur; silme onay modalı kalır.
  - Başlangıç durumu: hiçbir şey seçili değilken "Görevleri görmek için soldan bir kategori/alt kategori seçin." mesajı.
- Eski düz-liste filtre çubuğu (durum/birim/arama) kaldırılır; sayfanın birincil navigasyonu artık ağaç.

### 3.4 `Views/Tasks/MyTasks.cshtml` — checkbox kaldırma + tık ile filtre
- Sol ağaçtaki `.filter-checkbox` input'ları kaldırılır; kategori/alt kategori etiketleri tıklanabilir hale gelir.
- Tek seçim mantığı: alt kategoriye tıkla → orta (kartlar) ve sağ (zaman çizelgesi) o `subCategoryId`'ye göre filtrelenir; kategoriye tıkla → o kategorinin tümü. "Temizle" ile sıfırlanır. Aktif düğüm vurgulanır.
- Filtre halen **istemci tarafı** (kartlar/zaman çizelgesi zaten render edilmiş; `data-category-id`/`data-subcategory-id` üzerinden gizle/göster) — mevcut `applyFilter` çekirdeği korunur, sadece seçim kaynağı checkbox yerine tıklama olur.
- 3 sütunlu düzen (ağaç / aktif kartlar / zaman çizelgesi) korunur.

## 4. Doğrulama
- `dotnet build` ile derleme hatası olmadığını kontrol et.
- Manuel gözden geçirme: admin dışı kullanıcı `/Tasks`'a giderse `/Account/Denied`'e yönlenir; menüde "Görevler" görünmez.

## 5. Dokunulmayacaklar
- Application/Core/EntityFrameworkCore katmanları (API `SubCategoryId`/`CategoryId` filtresi zaten mevcut).
- Board, Completed, Create, Edit, Detail görünümleri.
