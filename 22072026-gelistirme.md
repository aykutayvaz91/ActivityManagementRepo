# ActivityManagement - Proje Gereksinim Dökümanı (Tam Versiyon)

> **Nasıl kullanılır:** Bu dosya sistemin mevcut yapısını ve yapılması istenen tüm güncellemeleri/ek geliştirmeleri kapsayan **tek ve bütünsel** gereksinim dökümanıdır.
> **Claude Code Talimatı:** Satır başındaki `[DEĞİŞTİ]` mevcut kural/yapı değişikliklerini, `[YENİ]` ise eklenen yeni modül ve özellikleri temsil eder. Lütfen tüm geliştirmeleri Bölüm 6'daki **Yazılım Kuralları ve Mimarisi** prensiplerine tam uyumlu şekilde uygulayın.

---

## 1. Genel Bakış ve Sistem Standartları

ABP Framework (ASP.NET Core MVC, .NET 8) tabanlı bir görev, faaliyet ve proje yönetim sistemi. 
* **Kaynak Kod:** `C:\ActivityManagement`
* **Canlı Site:** `C:\inetpub\ActivityManagement` (IIS, app pool: `ActivityManagement`, port 8090)

### Global Sistem Kuralları
* `[DEĞİŞTİ]` **Tarih ve Saat Formatı Standartlaştırması:** Sistem genelindeki tüm UI bileşenlerinde (DatePicker, Tablolar, Kartlar, Gantt, Takvim, Raporlar ve PDF/Excel çıktıları) tarih formatı kesin olarak **`dd.MM.yyyy` (Gün/Ay/Yıl)** veya **`dd/MM/yyyy`** standardına çekilecektir. `MM/dd/yyyy` (Ay/Gün/Yıl) kullanımı tamamen kaldırılacaktır.
* `[DEĞİŞTİ]` **Çoklu Takım (Multi-Team) Mimarisi:** Sistem; mevcut verilerin ve kullanıcıların altyapısını bozmadan, gelecekte çoklu takım yapısına kolayca evrilebileceği ve takımlar arası veri izolasyonu sağlayan modüler bir yapıda tasarlanacaktır.

---

## 2. Roller ve Yetki Yapısı

Roller `Employee.AppRole` alanında tutulur (ABP'nin kendi rol sisteminden bağımsız, cookie claim'i olarak taşınır):

| Rol | Açıklama |
|---|---|
| **Admin** | Tam yetki. Tüm takımların, projelerin, görevlerin, faaliyetlerin, kategorilerin ve sistem loglarının sahibidir. |
| **TakımLideri** | Kendi ekibinin işlerini yönetir, faaliyet konuları tanımlar, uzmanlardan gelen görevleri onaylar. Faz-1'de tüm projelere/görevlere erişebilir, Faz-2'de takım bazlı izole çalışır. |
| **Uzman** | Standart çalışan. Kendisine atanan görevleri/faaliyetleri yürütür, kendi açtığı görevler için lider onayı bekler, çalışma notu ve efor girişi sağlar. |

---

## 3. Modüller ve İşlevsel Gereksinimler

### 3.1 Personel (Employees) ve Kişi Kartı (360° Görünüm)
* **Yetkiler:** Personel ekleme/silme Admin/TakımLideri yetkisindedir. Uzman sadece kendi profil bilgilerini düzenleyebilir.
* `[YENİ]` **Kişi Kartı Detayı:** Kişi kartına tıklandığında açılan detay sayfasında/modalında personele ait tüm veriler konsolide olarak gösterilir:
  * Atanmış Aktif ve Geçmiş Görevler (Tasks)
  * Girilen Faaliyetler (Activity Logs) ve harcanan eforlar
  * Sorumlu olduğu / üyesi olduğu Projeler
* `[YENİ]` **Takvimde Göster Butonu:** Kişi kartının üst bölümünde yer alacak "Takvimde Göster" butonuna basıldığında, ilgili personelin kişisel ajandası/takvimi başka bir sayfada/sekmede açılacaktır.

### 3.2 Faaliyet (Activity) Yönetimi [YENİ]
SLA ve son teslim tarihine bağlı "Görevler" (Tasks) haricinde, rutin operasyonel veya periyodik işlerin takibi için SLA süresinden bağımsız **Faaliyet Konusu** ve **Faaliyet Girişi** yapısı kurulacaktır:

1. **Faaliyet Konusu Oluşturma (Takım Lideri / Admin):**
   * Takım Lideri, 13 sabit ana kategori ve alt kategori seçerek bir faaliyet konusu tanımlar ve bunu uzman arkadaşlara atar.
   * *Örnek:* Ana Kategori: `Sunucu & Sanallaştırma` -> Alt Kategori: `iDRAC` -> Faaliyet Konusu: `iDRAC Sağlık Kontrolleri ve Versiyon Güncellemeleri`.
2. **Faaliyet Girişi Yapma (Uzmanlar):**
   * Kendisine faaliyet konusu atanan uzman, belirlediği tarih (`dd.MM.yyyy`) için harcadığı süreyi (saat/dakika), yaptığı işin açıklamasını (Rich Text) ve varsa eklerini (ekran görüntüsü, dosya) sisteme efor kaydı olarak girer.

### 3.3 Görev Yönetimi, Akışlar ve Görünümler

#### A. Ana Sayfa Görünüm Modları (List, Gantt, Calendar) `[YENİ]`
Ana sayfa varsayılan olarak düzenli bir **Liste (Table View)** şeklinde açılır. Üst araç çubuğunda görünüm modları yer alır:
* **Liste (Varsayılan):** İşlerin tablo formatında sıralandığı görünüm.
* **Gantt Şeması (MS Project Tarzı):** Sol tarafta dikey olarak işlerin/projelerin hiyerarşik listesi (yukarıdan aşağıya), sağ tarafta ise zaman ekseninde (gün gün düzenli okunabilir takvim günleri) işlerin Başlangıç ve Bitiş tarihlerini gösteren renkli çubuklar (Gantt Chart).
* **Takvim Butonu:** Ajanda görünümünü ayrı sayfada açar.

#### B. Öz Görev Girişi ve Onay Mekanizması `[YENİ]`
* Bir Uzman kendisine veya sisteme yeni bir görev girdiğinde, görev doğrudan "Aktif" duruma geçmez; **`Onay Bekliyor` (Pending Approval)** statüsünde oluşturulur.
* Takım Lideri onayladığında görev resmileşir, kişi göreve dahil olur ve efor/not girişi sağlayabilir.

#### C. Zengin Metin Editörü (Rich Text), Yorum ve Dosya Yönetimi `[YENİ]`
Görev detay ekranındaki çalışma notu / yorum alanı aşağıdaki özelliklere sahip olacaktır:
* **Rich Text Editör:** Kalın (B), İtalik (I), Altı Çizili (U), Link, Görsel ve Biçimlendirme Temizleme butonları.
* **Görsel & Ekran Görüntüsü Desteği:** Pano üzerinden kopyala-yapıştır (`Ctrl+V`) veya dosya seçimi ile metin içerisine ekran görüntüsü gömebilme.
* **Dosya Eki (Attachment):** Editör altında isteğe bağlı dosya yükleme alanı (PDF, Log, Config, Zip vb.).
* **Dahili Not (Internal Note):** "Sadece temsilciler/liderler görebilir" mantığında gizli not düşebilme seçeneği (Checkbox).
* **Karakter/Kelime Sayacı & Yorum Akışı:** Anlık sayaç gösterimi ve altında kullanıcı bilgisi, tarih/saat damgası ve mention (`@kullanici`) içeren kronolojik yorum akışı.

### 3.4 Kategori Yönetimi
Veritabanı tohumlama (Seeding) ile aşağıdaki **13 sabit ana kategori** sisteme tanımlanır (İsimleri değiştirilemez, yalnızca Admin sorumlu atayabilir):
1. Sunucu & Sanallaştırma
2. Depolama & Yedekleme
3. Ağ & Bağlantı
4. Siber Güvenlik & Tehdit Yönetimi
5. Kimlik & Dizin Hizmetleri
6. Varlık, Lisans & Sertifika
7. Dosya, Bulut & İşbirliği
8. İş Uygulamaları & Veritabanı
9. Son Kullanıcı & Cihaz Yönetimi (Endpoint/MDM)
10. İzleme & Gözlemlenebilirlik
11. Eğitim
12. Yenilik & Araştırma
13. İdari & Operasyonel

*Alt kategoriler Admin ve TakımLideri tarafından eklenebilir/düzenlenebilir.*

### 3.5 Admin Loglama ve Denetim Modülü (Audit Logs) [YENİ]
Sadece **Admin** rolünün erişebileceği merkezi sistem denetim ekranı:
* **Loglanan Eylemler:** Kullanıcı giriş/çıkışları, yetki değişiklikleri, Görev/Faaliyet/Proje/Personel üzerindeki tüm Ekleme (Create), Güncelleme (Update) ve Silme (Delete) operasyonları, dosya yükleme/silme hareketleri.
* **Değişiklik Detayı (Diff View):** Güncelleme işlemlerinde eski ve yeni değerlerin karşılaştırılması (JSON Diff).
* **Filtreleme & Export:** Tarih aralığı (`dd.MM.yyyy`), Kullanıcı, İşlem Tipi ve Modül bazlı filtreleme + Excel/CSV export.

### 3.6 Raporlama Modülü (Report Center) [YENİ]
* **Filtreler:** Personel Bazlı, Ekip Bazlı, Aktivite/Görev Tipi Bazlı (Bakım, Destek, Arıza, Proje vb.), Tarih Aralığı (`dd.MM.yyyy`) ve Hızlı **Aylık Rapor** seçeneği.
* **Örnek Özet Çıktı:** *"Ahmet Yılmaz — 01.07.2026 - 31.07.2026 Tarihleri Arasında: 5 Adet Bakım İşi, 2 Adet Destek İşi, 1 Adet Güncelleme Faaliyeti tamamlamıştır. Toplam Harcanan Süre: 38 Saat."*
* **Export:** Rapor verilerinin Excel (`.xlsx`) ve PDF formatında indirilmesi.

### 3.7 E-posta & SMTP Sistem Ayarları (Admin Panel)
* Admin paneline SMTP Host, Port, Kullanıcı Adı/Şifre, SSL/TLS, Gönderen Adı/E-postası parametrelerinin konfigüre edilebileceği ve "Test E-postası Gönder" butonu içeren ayar ekranı.

---

## 4. İleri Seviye Geliştirme Önerileri (Roadmap)

1. **E-posta & Bildirim Sistemi:** Görev atandığında, SLA yaklaştığında veya yeni not eklendiğinde e-posta / in-app bildirim gönderilmesi.
2. **SLA & Gecikme Heatmap:** Planlanan vs gerçekleşen bitiş sürelerine göre gecikme haritası.
3. **Altyapı Nöbet / On-Call Yönetimi:** Vardiya/nöbet takviminin tanımlanması.

---

## 5. Veri Modeli Notları (Entity Schema)

* **`Team`:** `Id`, `Name`, `Description`, `LeaderId`, `IsActive`
* **`Employee`:** `FirstName`, `LastName`, `Title`, `Department`, `AppRole`, `Email`, `Phone`, `IsActive`, `UserId`, `TeamId`
* **`Project`:** `Name`, `Code`, `Description`, `StartDate`, `EndDate`, `PlannedEndDate`, `Status`, `Priority`, `ManagerId`, `TeamId`
* **`TaskItem`:** `Id`, `Title`, `Description`, `Status`, `ApprovalStatus` (*Pending/Approved/Rejected*), `Priority`, `TaskType` (*Bakım, Destek vb.*), `StartDate`, `DueDate`, `CompletedDate`, `AssignedEmployeeId`, `SecondaryEmployeeId`, `AssignedByEmployeeId`, `ParentTaskId`, `SubCategoryId`, `TeamId`
* **`ActivitySubject` `[YENİ]`:** `Id`, `Title`, `CategoryId`, `SubCategoryId`, `CreatedByLeaderId`, `AssignedEmployeeId`
* **`ActivityLog` `[YENİ]`:** `Id`, `ActivitySubjectId`, `EmployeeId`, `LogDate`, `DurationHours`, `Description` (RichText HTML), `Attachments`
* **`AuditLog` `[YENİ]`:** `Id`, `UserId`, `UserName`, `ExecutionTime`, `ClientIpAddress`, `ActionType`, `EntityName`, `EntityId`, `OriginalValues`, `NewValues`
* **`TaskNote` / `TaskComment`:** `Id`, `TaskItemId`, `SenderEmployeeId`, `RichNoteText` (HTML/Embedded Image), `IsInternal`, `CreatedAt`
* **`TaskAttachment`:** `Id`, `TaskItemId`, `TaskNoteId`, `FileName`, `FilePath`, `FileSize`, `UploadedByEmployeeId`, `UploadedAt`
* **`Category` & `SubCategory`:** `Name`, `Description`, `ResponsibleEmployee1Id`, `ResponsibleEmployee2Id`

---

## 6. Yazılım Kuralları ve Mimarisi (Software Coding Guidelines)

* **Clean Architecture / ABP Principles:** Domain (İş Kuralları), Application (AppServices/DTOs/AutoMapper), EntityFrameworkCore (DbContext/Repositories) ve Web (Lean Controllers/MVC UI) katman ayrımına kesinlikle uyulacaktır.
* **Çoklu Takım İzolasyonu:** `Project`, `TaskItem`, `Employee` entity'lerinde `TeamId` alanı tutulacak ve global query filter altyapısına uygun yazılacaktır.
* **Sorgu Standartları:** Read-only işlemlerde `.AsNoTracking()` zorunludur. Paging ve Filtering server-side (`PagedAndSortedResultRequestDto`) yapılmalıdır.
* **Async & DI Standartları:** Tüm I/O operasyonları `async/await` kalıbı ile yazılmalı, bağımlılıklar Constructor Injection ile verilmelidir.

---

## 7. Claude Code İçin Yapılacaklar Listesi (TODO)

- [ ] `dd.MM.yyyy` Tarih formatının tüm UI ve Backend DTO/Validator yapılarında global olarak set edilmesi.
- [ ] System `AuditLog` middleware/interceptor yapısının kurulması ve Admin paneline "Sistem Logları" ekranının yazılması.
- [ ] `ActivitySubject` ve `ActivityLog` DB Entity, Migration ve AppService katmanlarının geliştirilmesi.
- [ ] Takım Lideri Faaliyet Tanımlama ve Uzman Efor/Faaliyet Girişi ekranlarının yapılması.
- [ ] Kişi Kartı (Profile View) detay ekranının güncellenmesi ve "Takvimde Göster" butonunun eklenmesi.
- [ ] Ana Sayfa Liste görünümüne **MS Project tarzı Gantt Chart** modülünün eklenmesi.
- [ ] Uzmanların kendi açtıkları görevler için **Takım Lideri Onay Mekanizması** akışının kurulması.
- [ ] Görev Detay yorum alanına **Rich Text Editor**, **Ctrl+V Ekran Görüntüsü Yükleme** ve **Dosya Eki** desteğinin eklenmesi.
- [ ] Kişi, Ekip, Tarih Aralığı (Aylık) ve **Aktivite Tipi (Bakım, Destek vb.)** bazlı gelişmiş **Raporlama Ekranı** ve Excel Export fonksiyonunun yazılması.
- [ ] Admin Paneline "E-posta / SMTP Ayarları" sayfasının eklenmesi.