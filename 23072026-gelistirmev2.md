# ActivityManagement - Proje Gereksinim Dökümanı (23-07-2026 v3 Güncellemesi)

> **Dosya Adı:** `23072026-3-gelistirme.md`
> **Sistem Bilgisi:** ABP Framework (ASP.NET Core MVC, .NET 8) tabanlı görev, faaliyet ve proje yönetim sistemi.
> **Kaynak Kod:** `C:\ActivityManagement` | **Canlı Sistem:** `C:\inetpub\ActivityManagement`
> **Claude Code Talimatı:** Satır başındaki `[DEĞİŞTİ]` mevcut yapı/mantık değişikliklerini, `[YENİ]` ise eklenen yeni modül ve özellikleri temsil eder. Lütfen tüm geliştirmeleri Bölüm 8'deki **Yazılım Kuralları ve Mimarisi** prensiplerine tam uyumlu şekilde uygulayın.

---

## 1. Genel Bakış ve Sistem Standartları

* `[DEĞİŞTİ]` **Tarih Formatı Zorunluluğu (Global Culture Fix):** Sistem genelindeki tüm UI bileşenlerinde (DatePicker, Tablolar, Kartlar, Gantt, Takvim, Raporlar ve PDF/Excel çıktıları) tarih formatı **kesin olarak `dd.MM.yyyy` (Gün/Ay/Yıl)** standardına çekilecektir[cite: 1]. HTML input (`type="date"`), JavaScript DatePicker ve C# CultureInfo ayarları `MM/dd/yyyy` kabul etmeyecek şekilde global olarak güncellenecektir[cite: 1].
* `[YENİ]` **Sistem Bildirimleri / Toast Overlay Yapısı:** "Efor kaydı eklendi", "Görev güncellendi" gibi bildirim mesajları ekranın **sağ üst köşesinde overlay (yüzen) bir bildirim kutucuğu (Toast Notice)** olarak belirecek ve birkaç saniye sonra otomatik kaybolacaktır (`alert-banner` kullanılmayacaktır)[cite: 1].

---

## 2. Proje Yönetimi, Kategori Kalıtımı ve Sorumlu Otomasyonu

* `[YENİ]` **Projelerde Zorunlu Ana Kategori & Alt Kategori:**
  * Proje oluşturma/düzenleme formuna **Ana Kategori** ve **Alt Kategori** seçim alanları eklenecek ve zorunlu tutulacaktır[cite: 1].
* `[DEĞİŞTİ]` **Proje Görevlerinde Otomatik Kategori & Sorumlu Aktarımı:**
  * Bir projenin altına yeni görev girildiğinde, görevin **Ana Kategori** ve **Alt Kategori** alanları, bağlı olduğu projenin kategorilerinden **otomatik dolacak ve kilitlenecektir**[cite: 1].
  * Görevin **1. Atanan Kişisi** ve **2. Atanan Kişisi** alanları, bağlı olduğu projenin 1. ve 2. sorumlularından **otomatik çekilip doldurulacaktır**[cite: 1].
* `[YENİ]` **Projeden Faaliyet Girişi ve Kategori Oto-Doldurma:**
  * Faaliyet ekleme ekranında (veya doğrudan proje detayından "Faaliyet Ekle" dendiğinde) bir **Proje** seçilirse; sistem faaliyetin **Ana Kategori** ve **Alt Kategori** alanlarını seçilen projeden **otomatik dolduracaktır**[cite: 1].
  * Tüm görev ve faaliyet mimarisinde kategori sistemi ana omurga/baz olarak kullanılacaktır[cite: 1].
* `[YENİ]` **Alt Kategori Sorumluluk Matrisi (Asıl & Yedek Sorumlu):**
  * Takım lideri, ekibindeki herkes için alt kategoriler bazında sorumluluk dağılımını belirleyebileceği bir **Sorumluluk Matrisi** ekranına sahip olacaktır.
  * Her bir alt kategori için personele **Asıl Sorumlu** veya **Yedek Sorumlu** unvanı atanabilecektir. Tüm alt kategorilerin sorumlulukları bu matris üzerinden dağıtılabilecektir.
  * Görev grubu kutusu ve arayüzlerde ilgili personelin hangi alt kategorilerde Asıl, hangilerinde Yedek sorumlu olduğu açıkça görüntülenecektir.
* `[DEĞİŞTİ]` **Proje & Görev SLA Otomasyonu:**
  * Proje görevinde SLA tarihi manuel girilmezse, backend otomatik olarak projenin `SlaTargetDate` tarihini göreve SLA tarihi olarak atayacaktır[cite: 1].

---

## 3. Görev Mimarisi ve "Atayan Kişi" Mantığı

* `[DEĞİŞTİ]` **Atayan Kişi (AssignedBy) Otomasyonu:**
  * Bir görevi Takım Lideri veya Admin bir uzmana atıyorsa, **"Atayan Kişi"** alanı otomatik olarak işlemi yapan Takım Lideri/Admin seçilecektir[cite: 1].
  * Eğer bir Uzman/Kullanıcı **kendine görev giriyorsa**, "Atayan Kişi" otomatik olarak **kendisi** seçilecektir[cite: 1].
* `[DEĞİŞTİ]` **Takım Üyesi Ekleme / Sorumlu Seçimi UI Güncellemesi:**
  * Takım/Proje üyesi ekleme ekranında 1. Sorumlu ve 2. Sorumlu seçimleri **Checkbox** ile seçilebilir hale getirilecektir[cite: 1].
  * Diğer seçilen ekip üyeleri için ise yanlarında dropdown/input ile **Rol/Unvan** tanımı girilebilecektir[cite: 1].

---

## 4. Panodaki "Aktif Görevlerim" & Görev Ekleme Form Akıllandırma

* `[DEĞİŞTİ]` **Öncelik Sıralaması & Auto-Refresh:** "My Tasks" ekranı en yüksek öncelikten (Kritik/Yüksek) en düşüğe göre sıralanacaktır[cite: 1]. Sağ üstte varsayılanı **AÇIK** olan bir Auto-Refresh toggle switch yer alacaktır[cite: 1].
* `[YENİ]` **Görevlerim Ekranı Akıllı Varsayılanlar:**
  * **Filtreden Kategori Alma:** Sol TreeView'da hangi kategori seçiliyse, "Yeni Görev" denildiğinde formda o kategori otomatik seçili gelecektir[cite: 1].
  * **Sabit Takım:** Uzman kendine görev açarken takımı (ör. *Sistem Birimi*) kilitli gelecektir[cite: 1].
  * **Varsayılan Süre:** Yeni görevde süre alanı varsayılan **1 saat** gelecektir (0 saat kabul edilmeyecektir)[cite: 1].

---

## 5. Görev Sorgula, Raporlama ve Yetki Mimarisi

* `[DEĞİŞTİ]` **Dashboard & Multi-Team İzolasyonu:** Admin tüm sistemi görür; Takım Lideri ve Uzman yalnızca kendi takımının verisini görebilir[cite: 1].
* `[DEĞİŞTİ]` **"Görev Sorgula" Modülü (Eski Tamamlanmış Görevler):**
  * Durum, Tarih (`dd.MM.yyyy`), Görev Tipi ve Kategorilere göre detaylı sorgulama ekranıdır[cite: 1].
  * **Uzman:** Sadece kendisine ait görevleri sorgulayabilir[cite: 1].
  * **Takım Lideri:** Sadece kendi takımındaki kişilerin görevlerini sorgulayabilir[cite: 1].
  * **Admin:** Tüm sistem görevlerini sorgulayabilir[cite: 1].
* `[DEĞİŞTİ]` **Raporlama Yetkileri:** Uzman sadece **kendi efor/faaliyet raporunu**, Takım Lideri **kendi ekibinin raporunu**, Admin ise **tüm sistem raporunu** alabilir[cite: 1].

---

## 6. Görselleştirme, Görev Onay ve Rich Text Not Yönetimi

* `[DEĞİŞTİ]` **Kişi Kartı (360° Görünüm & Entegrasyonlar):**
  * Kişi detayında personele ait görevler, faaliyetler ve projeler konsolide olarak eksiksiz gösterilecektir[cite: 1].
  * **Proje Kontrolü Fix:** Proje oluşturulup takıma/projeye üye olarak eklenen personelin Kişi Kartı'nda bu projenin listelenmeme sorunu düzeltilecektir; kişinin bağlı olduğu tüm projeler (sorumlu veya takım üyesi olarak) kartında görünmelidir.
  * **Kategori & Sorumluluk Alanı:** Kişi kartında personelin sorumlu olduğu alanlar gösterilecektir (Örn: Sanallaştırma kategorisi altında *iDRAC* alt kategorisinin Asıl Sorumlusu: *Mustafa Keser*). Aynı şekilde Mustafa Keser'in kişi kartına girildiğinde "Sorumluluk Alanları" sekmesinde *iDRAC (Asıl Sorumlu)* bilgisi açıkça görüntülenecektir.
  * "Takvimde Göster" butonu ile kişinin ajandası yeni sekmede açılacaktır[cite: 1].
* **Ana Sayfa Görünümleri:** Liste (Varsayılan), **MS Project Tarzı Gantt Şeması** ve Takvim butonları[cite: 1].
* **Öz Görev Onayı:** Uzmanın kendi kendine oluşturduğu görevler `Onay Bekliyor` statüsünde açılır, Takım Lideri onaylayınca aktifleşir[cite: 1].
* **Rich Text Editör & Görsel:** Yorum alanında B/I/U, Bağlantı, `Ctrl+V` ekran görüntüsü yapıştırma, dosya eki ve "Sadece liderler görebilir" gizli not seçeneği[cite: 1].

---

## 7. Veri Modeli Güncellemeleri (Entity Schema)

* **`Project`:** `Id`, `Name`, `Code`, `Description`, `CategoryId`, `SubCategoryId`, `PrimaryResponsibleId`, `SecondaryResponsibleId`, `SlaTargetDate`, `Status`, `TeamId`[cite: 1]
* **`SubCategoryResponsibility` (Yeni):** `Id`, `SubCategoryId`, `EmployeeId`, `ResponsibilityType` (Enum: `Primary` / `Backup`), `AssignedByTeamLeaderId`
* **`TaskItem`:** `Id`, `Title`, `Description`, `Status`, `ApprovalStatus`, `Priority`, `TaskType`, `StartDate`, `DueDate`, `CategoryId`, `SubCategoryId`, `PrimaryAssignedEmployeeId`, `SecondaryAssignedEmployeeId`, `AssignedByEmployeeId`, `ProjectId`, `TeamId`, `EstimatedHours` (Default: 1)[cite: 1]
* **`ActivitySubject`:** `Id`, `Title`, `CategoryId`, `SubCategoryId`, `ProjectId`, `CreatedByLeaderId`, `AssignedEmployeeId`[cite: 1]
* **`ActivityLog`:** `Id`, `ActivitySubjectId`, `EmployeeId`, `LogDate`, `DurationHours`, `Description` (RichText HTML), `Attachments`[cite: 1]
* **`AuditLog`:** `Id`, `UserId`, `UserName`, `ExecutionTime`, `ClientIpAddress`, `ActionType`, `EntityName`, `EntityId`, `OriginalValues`, `NewValues`[cite: 1]

---

## 8. Yazılım Kuralları ve Mimarisi (Software Coding Guidelines)

* **Clean Architecture:** Domain, Application, EF Core ve Web katman mimarisine sıkı uyum[cite: 1].
* **Otomatik Veri Aktarımı (Backend Hooks):** Proje altından görev/faaliyet oluşturulurken frontend'den gelmese dahi backend seviyesinde (`TaskAppService`, `ActivityAppService`) Proje ID üzerinden Category ve Responsible alanlarının auto-populate edilmesi[cite: 1].
* **Validation & UI:** Tarihler global `dd.MM.yyyy` formatında; bildirimler sağ-üst yüzen Toast overlay yapısında çalışacaktır[cite: 1].

---

## 9. Claude Code İçin Yapılacaklar Listesi (TODO)

- [ ] Proje entity ve formlarına **Ana Kategori** ve **Alt Kategori** alanlarını zorunlu olarak eklemek[cite: 1].
- [ ] Proje altına görev girildiğinde Kategori ve 1./2. Sorumlu alanlarını projeden otomatik doldurup kilitlemek[cite: 1].
- [ ] Faaliyet ekleme ekranında proje seçilince kategorilerin projeden otomatik dolmasını sağlamak[cite: 1].
- [ ] **Sorumluluk Matrisi Ekranı:** Takım liderinin personellere Alt Kategori bazlı Asıl/Yedek Sorumlu atayabileceği ekranı ve arka plan veri yapısını (`SubCategoryResponsibility`) geliştirmek.
- [ ] **Kişi Kartı Düzeltmesi & Geliştirmesi:**
  - Projeye eklenen takım üyelerinin Kişi Kartı'nda ilgili projenin görünmesini sağlamak (ilişki sorgusu kontrolü).
  - Kişi kartında ve Kategori detayında Asıl/Yedek sorumlulukların (Örn: Sanallaştırma -> iDRAC -> Mustafa Keser) iki yönlü gösterimini entegre etmek.
- [ ] Görevi oluşturan/atayan kişi mantığını düzenlemek (Lider atıyorsa Lider, kişi kendi açıyorsa Kendisi)[cite: 1].
- [ ] Takım/Proje üyesi ekleme ekranında 1./2. Sorumluyu checkbox ile seçilebilir, diğer üyeleri rol girilebilir yapmak[cite: 1].
- [ ] Global `dd.MM.yyyy` tarih formatını ve sağ-üst Toast bildirimlerini sabitlemek[cite: 1].
- [ ] "Görev Sorgula" ve "Raporlama" ekranlarındaki Rol Bazlı (Admin/Lider/Uzman) erişim kısıtlarını yazmak[cite: 1].
- [ ] "My Tasks" auto-refresh, Gantt Chart, Rich Text Yorum ve Admin AuditLog modüllerini entegre etmek[cite: 1].