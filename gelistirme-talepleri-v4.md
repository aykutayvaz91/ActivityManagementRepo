# ActivityManagement - Sistem Geliştirme Gereksinim Dokümanı (V4 - Temmuz 2026)

> **Dosya Adı:** `gelistirme-talepleri-v4.md`  
> **Sistem Bilgisi:** ABP Framework (ASP.NET Core MVC, .NET 8)  
> **Mimari Yapı:** Clean Architecture (Domain, Application, EF Core, Web)  
> **Tarih Standardı:** Global `dd.MM.yyyy` (Tüm tarih gösterimleri ve girdi alanlarında zorunlu)

---

## 1. Görev & Deadline Yönetimi ve Esneklikler

* **Varsayılan Son Teslim Tarihi (Default Deadline):**
  * Yeni görev oluşturma ekranında **Deadline (Son Teslim Tarihi)** alanı otomatik olarak **"1 Sonraki Gün"** (`DateTime.Now.AddDays(1)`) olarak dolacaktır.
* **Süre Kısıtının Kaldırılması:**
  * Görev ve faaliyet girişlerinde "şu kadar saat sürecek / zaman kısıtı" zorunluluğu tamamen kaldırılacaktır.
  * İşin takvim üzerinde ilgili tarih aralığı/gün içerisinde yer alması yeterli kabul edilecektir.
* **Dinamik Önem Derecesi (1 - 10 Derecelendirme):**
  * Yönetici ve Takım Lideri görev tanımlarken veya düzenlerken **Önem Derecesi** `1` ile `10` arasında sayısal bir değer olarak seçebilecektir (1: En Düşük, 10: En Yüksek/Kritik).
  * **Sıralama Mantığı:** Görev listelerinde, panolarda ve takvimde görevler önem derecesine göre büyükten küçüğe sıralanacaktır. `10` önem derecesine sahip görevler listenin **en üstünde** görüntülenecektir.

---

## 2. Kategori Yönetimi ve Yetkilendirme

* **Admin Ana Kategori Yönetimi:**
  * Admin kullanıcıları sistem genelinde yeni **Ana Kategori** ekleme, düzenleme, pasife alma ve silme yetkisine sahip olacaktır.
* **Proje Oluşturma Yetkisi:**
  * **Çalışan (Uzman) Rolü:** Proje oluşturamaz.
  * Proje oluşturma yetkisi yalnızca **Admin** ve **Takım Lideri** rollerine tanınacaktır.

---

## 3. Faaliyet Tipleri (Activity Types) & Admin CRUD Modülü

* **Faaliyet Tipi (Activity Type) Yapısı:**
  * Görevlerde bulunan tip (Task Type) yapısı faaliyetlere de genişletilecektir.
  * Faaliyet girilirken *Destek*, *Bakım*, *Geliştirme*, *Toplantı*, *İnceleme* vb. **Faaliyet Tipleri** seçilecektir.
* **Admin Faaliyet Tipi Yönetim Ekranı:**
  * Admin panelinde Faaliyet Tiplerini dinamik yönetmek üzere **Ekle**, **Güncelle**, **Sil** (veya Pasife Al) fonksiyonlarını barındıran bir yönetim arayüzü eklenecektir.

---

## 4. Projeler Modülü ve Görünüm Sekmeleri

* **Projeler Sayfası İkili Sekme Yapısı:**
  * **Sekme 1: Tüm Projeler:** Yetki dahilindeki (veya tüm sistemdeki) projelerin listelendiği genel görünüm.
  * **Sekme 2: Projelerim:** Kullanıcının yalnızca **1. Sorumlu** veya **2. Sorumlu** olarak atandığı projelerin filtrelenerek listelendiği özel alan.

---

## 5. Otomatik Efor / Rutin Sistem Kontrolü Otomasyonu

* **Sorumlu Sistem Rutin Kontrol Eforu (8 Saat Tamamlama Otomasyonu):**
  * Personelin gün sonunda eksik kalan çalışma saatlerini tamamlamak ve rutin kontrolleri kayıt altına almak amacıyla otomatik efor tamamlama mekanizması kurulacaktır.
  * Personelin sorumlu olduğu sistemlerin rutin kontrolü için sistem tarafından otomatik olarak 1'er saatlik (vb.) faaliyet/efor kayıtları üretilecek veya öneri olarak sunulacak, günlük toplam eforun **8 saate** tamamlanması kolaylaştırılacaktır.

---

## 6. Raporlama, Günlük Faaliyet Özetleri ve Detaylı Kişi Bazlı İnceleme

* **Görev Sorgula Ekranı Filtre Genişletmesi:**
  * Görev Sorgula ekranındaki arama filtrelerine **Aktivite / Görev Tipi** ve **Faaliyet Tipi** kriterleri eklenecektir.
* **Raporlama Katmanı Ayrıştırması:**
  * Raporlar sayfasında veriler **Görev Tipleri**, **Faaliyet Tipleri** ve **Aktivite Türleri** bazında gruplanarak ve ayrıştırılarak sunulacaktır.
* **Detaylı Günlük Faaliyet Özeti:**
  * Günlük faaliyet özet raporunda, girilen işlerin **Alt Başlıkları** ve **Detay Notları** ayrı kolonlar/satırlar halinde detaylı şekilde gösterilecektir.
* **Kişi Bazlı Detaylı Çalışma / Efor Raporu ("Aykut Ne Yaptı?"):**
  * Yöneticilerin tek bir personelin (Örn: *Aykut*) belirli bir tarih aralığında hangi işlerle uğraştığını, hangi görevlerde ve faaliyetlerde ne kadar zaman harcadığını adım adım takip edebileceği **Kişisel Efor & Faaliyet Raporu** eklenecektir.

---

## 7. Arayüz (UI), Temalandırma, Logo ve Profil Fotoğrafı Yönetimi

* **Profil Fotoğrafı & Şirket Logosu:**
  * Kullanıcı (Personel) kartlarına ve profillerine **Profil Fotoğrafı** yükleme alanı eklenecektir.
  * Faaliyet Yönetimi ve Sistem Başlığı alanına yüklenmek üzere **Şirket Logosu (cmit logo)** desteği getirilecektir.
* **Dinamik Tema & RGB Renk Özelleştirme:**
  * Admin paneline tema ayarları ekranı eklenecektir.
  * Admin, sistemin ana rengini (örn. ana mavi tonları) **RGB / Hex renk kodu** seçerek dinamik olarak değiştirebilecektir.
  * Üst menü (TopView) ve genel tema bileşenleri seçilen renge göre dinamik CSS/Style injection ile güncellenecektir.

---

## 8. Claude Code / Geliştirici Yapılacaklar Listesi (TODO)

- [ ] Task Entity'sine `PriorityScore` (1-10) alanını eklemek ve listeleri önem derecesine göre azalan sırada (`OrderByDescending`) güncellemek.
- [ ] Görev açılışında `DueDate` varsayılanını yarının tarihi yapıp süre kısıtlarını kaldırmak.
- [ ] Admin panelinde Ana Kategori CRUD ve Faaliyet Tipi CRUD ekranlarını geliştirmek.
- [ ] Çalışan rolü için Proje Oluşturma butonunu pasife almak ve backend yetki kontrolü (`[Authorize]`) eklemek.
- [ ] Projelerim ekranına "Tüm Projeler" ve "Projelerim (1. ve 2. Sorumlu Olduklarım)" sekmelerini yerleştirmek.
- [ ] Günlük eforu 8 saate tamamlamak için sorumlu olunan sistemlere otomatik 1'er saatlik kontrol eforu oluşturma servisini yazmak.
- [ ] Görev Sorgula ve Raporlama modüllerine Aktivite/Faaliyet tipi filtrelerini entegre etmek.
- [ ] Günlük Faaliyet Özet tablosuna alt başlık ve detay not kolonlarını eklemek.
- [ ] Personel detaylı çalışma raporu ekranını ("Kullanıcı Ne Yaptı?") hayata geçirmek.
- [ ] Profil fotoğrafı yükleme, cmit logosu alanı ve Admin dinamik RGB tema rengi değiştirme altyapısını kurmak.
