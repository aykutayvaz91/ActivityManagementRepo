# ActivityManagement - Proje Gereksinim Dökümanı

> **Nasıl kullanılır:** Bu dosya sistemin şu anki gerçek davranışını yansıtır (kod okunarak çıkarıldı)[cite: 4]. Değiştirmek/eklemek istediğiniz yerleri düzenleyin[cite: 4]. Bir kuralı değiştiriyorsanız satırın başına `[DEĞİŞTİ]`, tamamen yeni bir istekse `[YENİ]` yazın ki uygularken gözden kaçırmayayım[cite: 4]. Emin olmadığınız yerleri `[?]` ile işaretleyip yanına not düşebilirsiniz, ben ona göre soru sorarım[cite: 4]. Dosyayı düzenleyip geri verdiğinizde, sadece işaretli/değişen kısımları koda geçiririm[cite: 4].

---

## 1. Genel Bakış

ABP Framework (ASP.NET Core MVC, .NET 8) tabanlı bir görev/faaliyet yönetim sistemi[cite: 4]. Kaynak kod: `C:\ActivityManagement`[cite: 4]. Canlı site: `C:\inetpub\ActivityManagement` (IIS, app pool: `ActivityManagement`, port 8090)[cite: 4].

`[DEĞİŞTİ]` Mevcut durumda tüm kullanıcılar tek bir "Infrastructure" (Altyapı) takımı çatısı altında çalışmaktadır[cite: 4]. Sistem mimarisi; mevcut verilerin ve kullanıcıların bu altyapıyı bozmadan, gelecekte çoklu takım (Multi-Team) yapısına kolayca evrilebileceği ve takımlar arası veri izolasyonu sağlayan modüler bir yapıda tasarlanacaktır[cite: 4].

---

## 2. Roller ve Takım Yapısı

Roller `Employee.AppRole` alanında tutulur (ABP'nin kendi rol sisteminden bağımsız, cookie claim'i olarak taşınır)[cite: 4]:

| Rol | Açıklama |
|---|---|
| **Admin** | Tam yetki[cite: 4]. Tüm takımların, projelerin, görevlerin ve kategorilerin sahibidir; ekleme, düzenleme ve silme yetkisine sahiptir[cite: 4]. |
| **TakımLideri** | `[DEĞİŞTİ]` Geçiş sürecinde tüm projelere ve görevlere erişabilir[cite: 4]. Faz-2'de (Çoklu Takım) sadece bağlı olduğu takımın projelerinden, görevlerinden ve üyelerinden sorumlu olacaktır[cite: 4]. |
| **Uzman** | Standart çalışan[cite: 4]. Sadece bağlı olduğu takımın ve/veya kendisine atanan işlerin üzerinde kısıtlı yetkiye sahiptir[cite: 4]. |

`[DEĞİŞTİ]` **Takım Dönüşüm Stratejisi (Faz 1 vs Faz 2):**[cite: 4]
- **Şimdiki Durum (Faz 1 - Tek Takım / Infrastructure):** Takım lideri ve mevcut 6 çalışan aynı takımda yer alır[cite: 4]. Tüm görev ve projeler ortak bir havuzda işlenir[cite: 4]. Takım Lideri genel yönetimsel operasyonları yürütür[cite: 4].
- **Gelecek Durum (Faz 2 - Çoklu Takım & İzolasyon):** Takımlar ayrılacaktır[cite: 4]. Her takım lideri sadece kendi ekibini, kendi takımına ait projeleri ve görevleri görebilecek/yönetebilecektir[cite: 4]. Admin rolü ise tüm takımlar üstünde globale yetkili tek rol olmaya devam edecektir[cite: 4].

---

## 3. Modüller ve Yetki Kuralları

### 3.1 Personel (Employees)

| İşlem | Kim yapabilir |
|---|---|
| Görüntüleme (liste, kişi kartı) | Herkes[cite: 4] |
| Oluşturma (yeni personel) | Admin, TakımLideri[cite: 4] |
| Düzenleme | Admin/TakımLideri → herkesi[cite: 4]. Uzman → **sadece kendi kaydını**, şu alanlar hariç: Rol (`AppRole`), Aktiflik (`IsActive`), Hesap bağlantısı (`UserId`), `[YENİ]` Takım ID (`TeamId`) — bunlar sadece Admin/TakımLideri tarafından değiştirilebilir[cite: 4]. |
| Silme | Admin, TakımLideri[cite: 4] |

### 3.2 Proje (Projects)

`[DEĞİŞTİ]` Projeler doğrudan bir takıma (`TeamId`) bağlanacaktır[cite: 4]. Şimdilik tüm projeler varsayılan olarak "Infrastructure" takımına atanır[cite: 4].

| İşlem | Kim yapabilir |
|---|---|
| Görüntüleme | Herkes (Faz-2'de sadece kendi takımının projeleri)[cite: 4] |
| Oluşturma | Admin, TakımLideri[cite: 4]. Uzman oluşturursa proje otomatik kendisine (`ManagerId`) atanır ve kendi takımına dahil edilir[cite: 4]. |
| Düzenleme | Admin (Tüm projeler), TakımLideri (Kendi takımının projeleri)[cite: 4] |
| Silme | Admin (Tüm projeler), TakımLideri (Kendi takımının projeleri)[cite: 4] |
| Proje üyesi ekleme/çıkarma | `[DEĞİŞTİ]` Sadece Admin ve Proje Yöneticisi (`ManagerId`) / İlgili Takım Lideri proje üyesi ekleyip çıkarabilir[cite: 4]. |

### 3.3 Görevler (Tasks) ve "Görevlerim" Ekran Mimarisi

- Üst görev (kök, `ParentTaskId = null`) oluşturma → sadece Admin/TakımLideri[cite: 4].
- Alt görev (mevcut bir üst görevin altına) oluşturma → herkes, ama Uzman oluşturursa otomatik kendine atanır[cite: 4].
- Görev düzenleme → Admin/TakımLideri, Uzman → sadece kendine atanmış görevleri[cite: 4].
- Görev silme → Admin/TakımLideri (her şeyi), Uzman → sadece kendi alt görevini (üst görev silemez)[cite: 4].
- **Kategori/Alt Kategori üzerinden görev ekleme:**[cite: 4]
  - `[DEĞİŞTİ]` **Sabit Ana Kategori Kısıtlaması:** Görev eklenirken dinamik veya serbest ana kategori seçimine izin verilmez[cite: 4]. Sistemde seçilebilecek ana kategoriler **yalnızca** Bölüm 3.4'te tanımlanan 13 alandan ibarettir[cite: 4].
  - Ana Kategori'nin 1. ve 2. sorumlusu (Employee) vardır[cite: 4].
  - Admin/TakımLideri → Herhangi bir alt kategoriye görev ekleyebilir[cite: 4].
  - Diğerleri → Sadece kendisinin 1./2. sorumlu olduğu ana kategorinin alt kategorilerine **kendine atayarak** görev ekleyebilir[cite: 4].

`[YENİ]` **"Görevlerim" Sayfası Layout Mimarisi (3 Bölmeli Düzen):**[cite: 4]

1. **Sol Bölme (Kategori TreeView Filtresi):**[cite: 4]
   - 13 Sabit Ana Kategori ve bunların altındaki Alt Kategorilerin sergilendiği bir **TreeView** (ağaç görünümü) bileşeni[cite: 4].
   - Kullanıcı buradan bir veya birden fazla alt kategori / ana kategori seçerek orta ve sağ bölmedeki aktif görevleri anlık olarak filtreleyebilir[cite: 4].

2. **Orta Bölme (Atanmış Görev Kartları / Task Cards):**[cite: 4]
   - Giriş yapan kullanıcıya (1. veya 2. sorumlu olduğu) atanmış **aktif** görevlerin **Kart (Box/Card)** formatında listelendiği alan[cite: 4].
   - Her kart üzerinde Görev Başlığı, Öncelik (Kritik/Yüksek/Normal/Düşük - renk kodlu), Son Tarih (SLA), Durum ve Bağlı Olduğu Kategori bilgisi yer alır[cite: 4].
   - Önceden belirlenmiş önem derecesi ve son tarihe göre sıralanır[cite: 4].

3. **Sağ Bölme (Zaman Çizelgesi & Takvim / Timeline View):**[cite: 4]
   - Dikey zaman akışına göre tasarlanmış takvim görünümü[cite: 4].
   - **Bugünden başlayıp geleceğe (yarına ve ileri tarihlere)** doğru kronolojik aşağıya akan bir takvim/çizelge yapısı[cite: 4].
   - Görevler başlangıç/bitiş veya SLA tarihlerine göre bu zaman çizelgesine bloklar/roster halinde yansıtılır[cite: 4].

`[YENİ]` **Tamamlanmış Görevler (Completed Tasks) Sayfası ve Menü Entegrasyonu:**[cite: 4]
- **Menü Erişimi:** Ana navigasyon menüsüne "Tamamlanmış Görevler" başlığı eklenecektir[cite: 4].
- **Ekran Tasarımı & Filtreleme:** Kapanmış/Tamamlanmış tüm görevlerin arşivlendiği gelişmiş veri tablosu/listesi[cite: 4].
- **Filtre Seçenekleri:**[cite: 4]
  - Tarih Aralığı (Tamamlanma Tarihine Göre Başlangıç - Bitiş)[cite: 4]
  - Ana Kategori & Alt Kategori Filtresi[cite: 4]
  - Yapan / Sorumlu Personel Filtresi[cite: 4]
  - Proje / Takım Filtresi[cite: 4]
  - Tamamlanma SLA Durumu (Gününde mi tamamlandı, gecikmeli mi kapandı?)[cite: 4]
- **Export / Raporlama:** Tamamlanan görevler listesi Excel (`.xlsx`) veya PDF formatında dışa aktarılabilir[cite: 4].

`[YENİ]` **Görev Detayı ve Aktivite Notları (Pop-up veya Yeni Sekme / Direct Link):**[cite: 4]
- **Etkileşim:** Görev kartına, takvim bloğuna veya tamamlanmış görev satırına tıklandığında görev detayı açılır (Pop-up modal veya doğrudan ayrı bir tab/sayfada `Direct Link` ile)[cite: 4].
- **Detay İçeriği:** Görevin tüm alanları (Açıklama, Atananlar, Tarihler, Kategori, Durum, Öncelik) sergilenir[cite: 4].
- **Tarihçe / Notlar ve Mesajlaşma Alanı (Task Activity Feed & Comments):**[cite: 4]
  - Görev detay ekranının alt kısmında, görev üzerinde yapılan çalışmaların, ilerleme notlarının ve güncellemelerin eklenebildiği bir **Mesaj Kutusu / Not Giriş Alanı** bulunur[cite: 4].
  - Kullanıcılar buraya tarih/saat damgalı, kimin yazdığı belirgin olan çalışma notları, loglar veya durum mesajları ekleyebilir (Kronolojik akış şeklinde listelenir)[cite: 4].

### 3.4 Kategori Yönetimi & Sistem Ayarları (Admin Panel)

`[DEĞİŞTİ]` Ana kategoriler dinamik olarak eklenip silinemez[cite: 4]. Veritabanı tohumlama (Seeding / Migration) ile aşağıdaki **13 sabit ana kategori** sisteme tanımlanır[cite: 4]:

1. Sunucu & Sanallaştırma[cite: 4]
2. Depolama & Yedekleme[cite: 4]
3. Ağ & Bağlantı[cite: 4]
4. Siber Güvenlik & Tehdit Yönetimi[cite: 4]
5. Kimlik & Dizin Hizmetleri[cite: 4]
6. Varlık, Lisans & Sertifika[cite: 4]
7. Dosya, Bulut & İşbirliği[cite: 4]
8. İş Uygulamaları & Veritabanı[cite: 4]
9. Son Kullanıcı & Cihaz Yönetimi (Endpoint/MDM)[cite: 4]
10. İzleme & Gözlemlenebilirlik[cite: 4]
11. Eğitim[cite: 4]
12. Yenilik & Araştırma[cite: 4]
13. İdari & Operasyonel[cite: 4]

> **Kategori Yönetim Kuralları:**[cite: 4]
> - **Ana Kategoriler:** Yalnızca Admin tarafından sorumlu ataması yapılabilir (İsmi/Listesi değiştirilemez)[cite: 4].
> - **Alt Kategoriler:** Admin ve TakımLideri bu ana kategorilerin altına yeni alt kategoriler ekleyebilir, düzenleyebilir veya silebilir[cite: 4].

`[YENİ]` **E-posta & SMTP Sistem Ayarları Ekranı (Admin Management):**
- Admin panelinde sadece Admin rolünün erişebileceği bir **"Sistem / E-posta Ayarları" (Email Settings)** sekmesi/ekranı yer alacaktır.
- Sistem üzerinden atılacak tüm otomatik e-postalar için aşağıdaki konfigürasyon parametreleri bu arayüzden yönetilecektir:
  - **Gönderen Mail Adresi (Sender Mail Address):** E-postanın hangi adresten atılacağı (Örn: `noreply@sirketiniz.com` veya `aktif@sirketiniz.com`).
  - **Gönderen Adı (Sender Display Name):** Maillerde görünecek başlık (Örn: `ActivityManagement Bildirim Sistemi`).
  - **SMTP Sunucu Adresi (Host):** Mail sunucusu IP veya domain adresi (Örn: `smtp.office365.com` / `mail.sirketiniz.com`).
  - **SMTP Port:** Kullanılacak port numarası (Örn: `587`, `465`, `25`).
  - **Kullanıcı Adı & Parola:** SMTP yetkilendirme bilgileri.
  - **SSL/TLS Kullanımı:** Güvenli bağlantı seçeneği (Enable SSL - True/False).
- **Test E-postası Gönder Butonu:** Ayarların doğruluğunu anlık test etmek için istenen bir mail adresine deneme e-postası atan fonksiyon.

---

## 4. Önerilen İleri Seviye Geliştirme Maddeleri (Roadmap & Feature Suggestions)

Sistemin kurumsallığını ve verimliliğini artırmak adına Altyapı/Infrastructure ekibi için projeye eklenebilecek mimari ve işlevsel geliştirme önerileri[cite: 4]:

1. **Dosya & Ek (Attachment) Yönetimi:**[cite: 4]
   - Görevin veya altına eklenen çalışma notlarının içerisine ekran görüntüsü, konfigürasyon dosyası, PDF veya log dosyası yüklenebilmesi (`TaskAttachment` entity)[cite: 4].
2. `[DEĞİŞTİ]` **E-posta & Bildirim Sistemi (Notification Center):**[cite: 4]
   - Admin Panel'de konfigüre edilen SMTP/E-posta ayarları kullanılarak kullanıcıya yeni görev atandığında, görevin SLA süresi yaklaştığında (örn. son 24 saat), üzerine yeni not yazıldığında veya durum değiştiğinde (In Progress → Completed) e-posta ve sistem içi (In-App) bildirim gönderilmesi[cite: 4].
3. **SLA & Gecikme Takibi (SLA Management & Heatmap):**[cite: 4]
   - Tamamlanan veya devam eden görevlerin "Planlanan Bitiş Tarihi" ile "Gerçekleşen Bitiş Tarihi" kıyaslanarak gecikme oranlarının hesaplanması, geciken görevlerin kırmızı/uyarı renk tonu ile görselleştirilmesi[cite: 4].
4. **Altyapı Nöbet / On-Call Yönetimi:**[cite: 4]
   - Altyapı (Infrastructure) ekiplerinde yaygın olan mesai dışı nöbet/vardiya takviminin sistem üzerinde tanımlanabilmesi ve nöbetçi uzmana otomatik acil görev yönlendirilmesi[cite: 4].
5. **Aktivite ve Şeffaflık Logları (Audit Logging / History):**[cite: 4]
   - Bir görevin kim tarafından ne zaman açıldığı, durumu ne zaman değiştirdiği, atanan kişinin ne zaman değiştirildiği gibi tüm sistem hareketlerinin değiştirilemez bir tarihçede (`TaskAuditLog`) tutulması[cite: 4].
6. **Ekip Dashboard & Performans Raporları:**[cite: 4]
   - Takım Lideri ve Admin için: Kategorilere göre görev dağılım grafikleri, kişi başına düşen aktif/tamamlanan görev sayıları, ortalama görev kapatma süreleri (MTTR - Mean Time to Resolution) gösteren interaktif dashboard[cite: 4].

---

## 5. Yazılım Kuralları ve Mimarisi (Software Coding Guidelines)

Projenin ASP.NET Core MVC (ABP Framework) mimarisinde temiz, bakımı kolay ve gelecekteki çoklu takım dönüşümüne uyumlu kalması için aşağıdaki yazılım kurallarına kesinlikle uyulacaktır[cite: 4]:

### 5.1 Mimari ve Katman Sorumlulukları (Clean Architecture / ABP Principles)
* **Domain Layer (Etki Alanı Katmanı):** İş kuralları, Entity'ler, Value Object'ler ve Domain Service'ler burada yer alır[cite: 4]. Veritabanı veya UI bağımlılığı barındırmaz[cite: 4].
* **Application Layer (Uygulama Katmanı):** UI'dan gelen istekleri karşılayan `ApplicationService` sınıfları burada bulunur[cite: 4]. DTO (Data Transfer Object) dönüşümleri `AutoMapper` ile yapılır[cite: 4]. Veritabanı entity'leri asla UI'a (Controller/View) direkt olarak açılmaz[cite: 4].
* **EntityFrameworkCore (Altyapı Katmanı):** DbContext, Repository implementasyonları ve Fluent API konfigürasyonları bu katmandadır[cite: 4].
* **Web (UI / MVC Katmanı):** Yalnızca sunum mantığı içerir[cite: 4]. Controller'lar lean (ince) tutulur; iş mantığı yazılmaz, doğrudan `ApplicationService` metodlarını çağırır[cite: 4].

### 5.2 Veri Verimliliği ve İzolasyonu Kuralları
* **Çoklu Takım Mimarisine Hazırlık (Team Data Isolation Filter):**[cite: 4]
  * `Project`, `TaskItem` ve `Employee` entity'lerine `TeamId` (Guid, nullable) alanı eklenmelidir[cite: 4].
  * İleride takımlar ayrıldığında ABP Framework'ün `IDataFilter` / Global Query Filter mekanizması devreye alınarak, kullanıcıların veritabanı seviyesinde sadece kendi takımlarına ait `TeamId` verilerini sorgulaması sağlanacaktır (`MustHaveTeam` veya `MayHaveTeam`)[cite: 4].
* **Veri Çekme (Sorgu) Standartları:**[cite: 4]
  * Sadece okuma (Read-only) yapılan View/Index senaryolarında mutlaka `.AsNoTracking()` kullanılmalıdır[cite: 4].
  * Sayfalama (Paging) ve Filtreleme, ABP'nin `PagedAndSortedResultRequestDto` yapısı kullanılarak **Server-Side (EF Core seviyesinde)** yapılmalıdır[cite: 4]. Tüm liste belleğe çekilip (`ToList()`) RAM'de filtreleme **yapılamaz**[cite: 4].

### 5.3 Kodlama, Naming ve Tasarım Desenleri
* **C# / Async Standartları:** Tüm I/O (Veritabanı, DTO eşleme, dış servis) işlemleri `async / await` kalıbı ile yazılmalıdır (`ToListAsync`, `FirstOrDefaultAsync` vb.)[cite: 4].
* **Bağımlılık Enjeksiyonu (Dependency Injection):** Sınıflar ABP'nin otomasyon arayüzlerini türetmelidir (`ITransientDependency`, `ISingletonDependency`, `IScopedDependency`)[cite: 4]. Yapıcı metot (Constructor Injection) tercih edilmelidir[cite: 4].
* **DTO ve Validation:** Sunum katmanına taşınan veriler için kesinlikle DTO kullanılmalıdır[cite: 4]. Gelen isteklerin doğrulaması FluentValidation veya DataAnnotations ile DTO seviyesinde yapılmalıdır[cite: 4].
* **Yetkilendirme (Authorization):** Controller veya AppService metodlarında `[Authorize]` veya ABP'nin `Permission` yapısı kullanılmalıdır[cite: 4]. Kod içinde role bazlı `if (User.IsInRole(...))` spagettisinden kaçınılmalı, bunun yerine declarative policy/permission tercih edilmelidir[cite: 4].

---

## 6. Veri Modeli Notları (Güncellendi)

- `Team`: Id, Name, Description, LeaderId, IsActive[cite: 4].
- `Employee`: FirstName, LastName, Title, Department, AppRole, ExpertiseAreas, Email, Phone, IsActive, `UserId`, `TeamId`[cite: 4].
- `Project`: Name, Code, Description, StartDate/EndDate/PlannedEndDate, Status, Priority, `ManagerId`, `TeamId`[cite: 4].
- `TaskItem`: Title, Description, Status, Priority, StartDate/DueDate/CompletedDate, `AssignedEmployeeId`, `SecondaryEmployeeId`, `AssignedByEmployeeId`, `ParentTaskId`, `SubCategoryId`, `TeamId`[cite: 4].
- `TaskNote` / `TaskComment`: Id, TaskItemId, SenderEmployeeId, NoteText, CreatedAt[cite: 4].
- `TaskAttachment`: Id, TaskItemId, FileName, FilePath, FileSize, UploadedByEmployeeId, UploadedAt[cite: 4].
- `Category`: Name (Sabit Enum/Lookup), Description, `ResponsibleEmployee1Id`, `ResponsibleEmployee2Id`[cite: 4].
- `SubCategory`: Name, Description, `CategoryId`[cite: 4].
- `EmailSettings` / `SettingManagement` `[YENİ]`: SmtpHost, SmtpPort, SmtpUserName, SmtpPassword, SmtpEnableSsl, SenderEmail, SenderDisplayName.

---

## 7. Açık Sorular / Yapılacaklar (TODO)

- [x] ~~`[?]` Admin / TakımLideri yetki ayrımı ve Proje üyesi kısıtı~~ (Netleştirildi: Geçiş sürecinde TakımLideri Infrastructure ekibini yönetir, Faz-2'de izolasyon sağlanacak. Proje üyesini sadece Admin ve Proje Yöneticisi değiştirebilir)[cite: 4].
- [ ] `[YENİ]` Admin Paneline "E-posta / SMTP Ayarları" yönetim sayfasının eklenmesi ve ABP `ISettingManager` veya `EmailSettings` tablosu entegrasyonu.
- [ ] `[YENİ]` "Tamamlanmış Görevler" sayfası ve navigasyon menü linkinin eklenmesi (Tarih, Kategori, Personel filtreli + Excel Export)[cite: 4].
- [ ] `[YENİ]` "Görevlerim" sayfasındaki 3 bölmeli layout (TreeView, Kartlar, Zaman Çizelgesi Takvimi) tasarımının ve API'lerinin yazılması[cite: 4].
- [ ] `[YENİ]` Görev Detay Pop-Up / Sayfası ve altındaki `TaskNote` (Aktivite mesaj kutusu) veritabanı tablosu ile UI entegrasyonunun yapılması[cite: 4].
- [ ] `[YENİ]` Migration ile 13 Sabit Ana Kategori'nin DB Seed betiğinin hazırlanması[cite: 4].
- [ ] `[YENİ]` Entity'lere `TeamId` eklenmesi ve mevcut verilerin "Infrastructure" takımıyla ilişkilendirilmesi[cite: 4].
- [ ] `[YENİ]` Modüllerin Yazılım Kuralları (Bölüm 5) standartlarına göre refactor edilmesi[cite: 4].