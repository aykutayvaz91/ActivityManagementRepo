# ActivityManagement - Proje Gereksinim Dökümanı (23-07-2026 Güncellemesi)

> **Dosya Adı:** `23-07-2026-gelistirme.md`
> **Sistem Bilgisi:** ABP Framework (ASP.NET Core MVC, .NET 8) tabanlı görev, faaliyet ve proje yönetim sistemi.
> **Kaynak Kod:** `C:\ActivityManagement` | **Canlı Sistem:** `C:\inetpub\ActivityManagement`
> **Claude Code Talimatı:** Satır başındaki `[DEĞİŞTİ]` mevcut yapı/mantık değişikliklerini, `[YENİ]` ise eklenen yeni modül ve özellikleri temsil eder. Lütfen tüm geliştirmeleri Bölüm 7'deki **Yazılım Kuralları ve Mimarisi** prensiplerine tam uyumlu şekilde uygulayın.

---

## 1. Genel Bakış ve Sistem Standartları

* `[DEĞİŞTİ]` **Tarih Formatı Zorunluluğu (Global Culture/DatePicker Fix):** Sistem genelindeki tüm UI bileşenlerinde (DatePicker, Tablolar, Kartlar, Gantt, Takvim, Raporlar ve PDF/Excel çıktıları) tarih formatı **kesin olarak `dd.MM.yyyy` (Gün/Ay/Yıl)** veya **`dd/MM/yyyy`** standardına çekilecektir. HTML input (`type="date"`), JavaScript DatePicker ve C# CultureInfo ayarları `MM/dd/yyyy` kabul etmeyecek şekilde global olarak güncellenecektir.
* `[YENİ]` **Sistem Bildirimleri / Toast Overlay Yapısı:** "Efor kaydı eklendi", "Görev güncellendi" gibi işlem başarı/bilgilendirme mesajları sayfanın üstünü itip aşağı kaydırmayacaktır (`alert-banner` kullanılmayacaktır). Bunun yerine ekranın **sağ üst köşesinde overlay (yüzen) bir bildirim kutucuğu (Toast Notice)** olarak belirecek ve birkaç saniye sonra otomatik kaybolacaktır.

---

## 2. Roller, Yetki Mimarisi ve Takım İzolasyonu (Multi-Team / Dashboard)

Roller `Employee.AppRole` alanında tutulur (ABP cookie claim'i olarak taşınır).

* `[DEĞİŞTİ]` **Dashboard & Veri İzolasyon Mantığı (Role-Based Visibility):**
  * **Admin Rolü:** Ana sayfaya/Dashboard'a girdiğinde sistemdeki tüm takımların, projelerin, görevlerin, personellerin ve logların verisini görür.
  * **Takım Lideri ve Uzman Rolü:** Ana ekrana/Dashboard'a girdiğinde **yalnızca bağlı bulunduğu takımın** kişilerini, projelerini, görevlerini ve faaliyetlerini görebilir. Farklı takımların verileri veritabanı seviyesinde filtrelenerek gizlenir.

---

## 3. Proje Yönetimi ve SLA Otomasyonu

* `[DEĞİŞTİ]` **Proje Sorumluları (1. Sorumlu & 2. Sorumlu):**
  * Proje oluşturma/düzenleme ekranında eski "Proje Yöneticisi" tekli seçim alanı ve ekip ekleme altındaki checkbox kaldırılacaktır.
  * Yerine **"1. Sorumlu"** ve **"2. Sorumlu"** alanları getirilecek ve bu alanların her birine **tek bir kişi** atanabilecektir.
* `[YENİ]` **Proje Görevlerinde Sorumlu Oto-Atama:**
  * Bir projenin altına yeni bir görev (Task) girildiğinde, görevin **1. Atanan Kişisi** ve **2. Atanan Kişisi** alanları, bağlı olduğu projenin 1. ve 2. sorumlularından **otomatik olarak doldurulacaktır**.
* `[DEĞİŞTİ]` **Proje & Görev SLA Otomasyonu:**
  * **Proje Seviyesinde SLA:** Projelerin kendine ait zorunlu SLA / Hedef Bitiş Süreleri olacaktır.
  * **Görevlerde Otomatik Son Tarih Mirası:** Bir projeye alt görev eklenirken arayüzde SLA/Son Teslim tarihi girilmese dahi, **arkataraf (backend) otomatik olarak projenin son tarihini bu görevin SLA/Son Tarihi olarak set edecektir**. Böylece "Zaman Çizelgesi" (Timeline/Gantt) tarafında "son tarihi olmayan aktif görev" hatalarının önüne geçilecektir.
  * **Arayüz Ayrımı:** Proje detayından görev eklerken SLA alanı muaf tutulup gizlenirken, standart (projesiz) görev ekleme sayfasında SLA alanı görünmeye devam edecektir.

---

## 4. Panodaki "Aktif Görevlerim" (My Tasks) & Görev Ekleme Akıllı Formu

* `[DEĞİŞTİ]` **Öncelik Sıralaması:** "My Tasks" (Aktif Görevlerim) sayfasındaki görevler **en yüksek öncelikten (Kritik/Yüksek) en düşüğe (Normal/Düşük)** doğru otomatik sıralanarak listelenecektir.
* `[YENİ]` **Otomatik Yenileme (Auto-Refresh Toggle):**
  * "My Tasks" sayfasına sağ üst köşeye bir **"Otomatik Yenile" (Auto-Refresh)** toggle switch konulacaktır.
  * Toggle varsayılan olarak **AÇIK (Default: Active)** gelecektir (istendiğinde kapatılabilir).
  * Açık olduğunda sayfa periyodik olarak yenilenecek, yeni görev atandığında anında ekrana yansıyacaktır.
* `[YENİ]` **Görevlerim Ekranı Akıllı Form & Varsayılan Değerler:**
  * **Otomatik Kategori Seçimi:** Görevlerim sayfasında soldaki TreeView'da seçili olan Ana Kategori ve Alt Kategori filtresi ne ise, "Yeni Görev" butonuna basıldığında açılan formda bu kategoriler **otomatik seçili** gelecektir.
  * **Sabit Takım Seçimi:** Uzman kendi kendine görev eklerken, takımı otomatik olarak bağlı olduğu birim (örneğin *Sistem Birimi*) olarak kilitli gelecektir, değiştirilemeyecektir.
  * **Varsayılan Zaman:** Yeni görev formunda zaman/süre alanı **default olarak 1 saat** ayarlanacaktır (0 saat olamaz, hata kontrolleri eklenmelidir).

---

## 5. Görev Sorgula (Eski Tamamlanmış Görevler) & Raporlama Yetkileri

* `[DEĞİŞTİ]` **"Görev Sorgula" Modülü (Yetki Bazlı Filtreleme):**
  * "Tamamlanmış Görevler" menüsünün adı **"Görev Sorgula"** olarak değiştirilecektir.
  * Bu ekranda Durum (Tamamlandı, Devam Ediyor, İptal), Tarih Aralığı (`dd.MM.yyyy`), Görev Tipi (Bakım, Destek vb.), Kategori ve Personel gibi tüm parametrelere göre esnek arama yapılabilecektir.
  * **Uzman Rolü:** Bu sayfada **sadece kendisine ait** görevleri sorgulayıp listeleyebilir.
  * **Takım Lideri Rolü:** Sadece **kendi takımındaki kişilerin** görevlerini sorgulayabilir.
  * **Admin Rolü:** Sistemdeki tüm görevleri sorgulayabilir.
* `[DEĞİŞTİ]` **Raporlama Modülü Yetki Kuralları:**
  * **Admin:** Tüm sistem veya takım bazlı kapsamlı raporlar çekebilir.
  * **Takım Lideri:** Kendi ekibine/takımına ait kişilerin raporlarını çekebilir.
  * **Uzman:** Sadece **kendi kişisel çalışma/efor raporunu** çekebilir. Başka bir personelin veya ekibin raporunu çekemez.

---

## 6. Görselleştirme, Görev Onay ve Rich Text Not Yönetimi

### 6.1 Kişi Kartı ve Görünüm Modları
* **Kişi Kartı (360° Görünüm):** Kişi detayında atanan görevler, girilen faaliyetler ve projeler tek pencerede derlenir. Üst kısımda **"Takvimde Göster"** butonu bulunur ve tıklanınca kişinin ajandası farklı sekmede açılır.
* **Ana Sayfa Görünümleri:** Liste (Varsayılan), **MS Project Tarzı Gantt Şeması** (sol dikey listede işler, sağda zaman ekseninde renkli gün blokları) ve Takvim görünümü geçiş butonları toolbar'da yer alır.

### 6.2 Öz Görev Onay Süreci
* Uzmanın kendi kendine oluşturduğu görevler **`Onay Bekliyor`** statüsünde açılır. Takım Lideri panelinden onayladığında görev aktife çekilir.

### 6.3 Rich Text Editör, Yorum ve Görsel Desteği
 Görev detaylarındaki yorum/çalışma notu alanı aşağıdaki özelliklere sahiptir:
* **Editör Özellikleri:** Kalın, İtalik, Altı Çizili, Bağlantı, Görsel ekleme ve Metin Temizleme butonları.
* **Panodan Yapıştırma (Ctrl+V):** Ekran görüntüleri doğrudan yorum kutusuna kopyala-yapıştır ile yüklenebilir.
* **Dosya Eki & Dahili Not:** PDF, Log vb. ekleme alanı ve "Sadece liderler görebilir" gizli not checkbox seçeneği.

---

## 7. Faaliyetler ve Admin Loglama

* **SLA'den Bağımsız Faaliyetler (Activities):** Takım Lideri'nin tanımladığı periyodik/rutin Faaliyet Konularına (ör. *iDRAC Sağlık Kontrolü*) uzmanların efor/saat ve açıklama kaydı girmesi modülü.
* **Admin Loglama Modülü (Audit Logs):** Sadece Admin rolünün erişebileceği; Kullanıcı Giriş/Çıkış, CRUD (Ekle/Sil/Güncelle) işlemleri, Yetki ve Dosya hareketlerinin tutulduğu, eski/yeni değer farklarını (Diff View) gösteren merkezi log ekranı.

---

## 8. Veri Modeli Güncellemeleri (Entity Schema)

* **`Project`:** `Id`, `Name`, `Code`, `Description`, `PrimaryResponsibleId`, `SecondaryResponsibleId`, `SlaTargetDate`, `Status`, `TeamId`
* **`TaskItem`:** `Id`, `Title`, `Description`, `Status`, `ApprovalStatus`, `Priority`, `TaskType`, `StartDate`, `DueDate` (Proje görevlerinde boş bırakılırsa otomatik `Project.SlaTargetDate` atanır), `PrimaryAssignedEmployeeId`, `SecondaryAssignedEmployeeId`, `ProjectId`, `TeamId`, `EstimatedHours` (Default: 1)
* **`ActivitySubject`:** `Id`, `Title`, `CategoryId`, `SubCategoryId`, `CreatedByLeaderId`, `AssignedEmployeeId`
* **`ActivityLog`:** `Id`, `ActivitySubjectId`, `EmployeeId`, `LogDate`, `DurationHours`, `Description` (RichText HTML), `Attachments`
* **`AuditLog`:** `Id`, `UserId`, `UserName`, `ExecutionTime`, `ClientIpAddress`, `ActionType`, `EntityName`, `EntityId`, `OriginalValues`, `NewValues`
* **`TaskNote` / `TaskComment`:** `Id`, `TaskItemId`, `SenderEmployeeId`, `RichNoteText`, `IsInternal`, `CreatedAt`

---

## 9. Yazılım Kuralları ve Mimarisi (Software Coding Guidelines)

* **Clean Architecture:** Domain, Application, EF Core ve Web katman sorumluluklarına sıkı sıkıya bağlı kalınacaktır.
* **Veri & Rapor İzolasyonu:** Rapor ve Görev Sorgulama backend AppService metodlarında `CurrentEmployee.AppRole` ve `TeamId` kontrolleri yapılarak yetkisiz veri erişimi kesin olarak engellenecektir.
* **UI/UX & Validation:** Sağ-üst Toast bildirimler kullanılacak; tarih biçimleri global JS/C# culture seviyesinde `dd.MM.yyyy` olarak kilitlenecektir.

---

## 10. Claude Code İçin Yapılacaklar Listesi (TODO)

- [ ] Global Culture, JS DatePicker ve HTML Input formatlarını `dd.MM.yyyy` olarak sabitlemek.
- [ ] Raporlama ve "Görev Sorgula" sayfalarına Rol Bazlı Yetkilendirme (Admin -> Hepsi, Lider -> Ekip, Uzman -> Sadece Kendisi) kısıtlarını yazmak.
- [ ] Proje altına eklenen görevlerde SLA girilmemişse arka planda projenin SLA tarihini atayan otomatik güncellemeyi eklemek.
- [ ] "Görevlerim" sayfasındaki TreeView filtre seçimlerini "Yeni Görev" modal formuna otomatik aktaran mantığı kurmak.
- [ ] Uzman görev açarken takım bilgisini otomatik getirmek ve zaman alanını varsayılan 1 saat olarak set edip validation kontrollerini eklemek.
- [ ] Global notify/toast mekanizmasını sağ-üst overlay yapıya geçirmek.
- [ ] "My Tasks" ekranına öncelik sıralaması ve varsayılanı AÇIK olan Auto-Refresh toggle bileşenini entegre etmek.
- [ ] Admin `AuditLog` modülünü, Faaliyet Yönetimini, MS Project tipli Gantt Chart ve Rich Text Yorum alanlarını entegre etmek.