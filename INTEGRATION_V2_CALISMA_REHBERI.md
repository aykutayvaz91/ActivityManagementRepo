# 📚 CMIT Destek Sistemi ⇄ Faaliyet Yönetim Sistemi V2 Entegrasyon Rehberi

Bu döküman, **destek.cmit.com.tr (Cortex Suite)** ile **TDV/CMIT Altyapı Ekibi — Faaliyet Yönetim Sistemi (`activitymanagement.tdv.org`)** arasındaki **V2 Çift Yönlü REST API Entegrasyonunun** çalışma prensiplerini, veri akışını, e-posta/bildirim tetikleme mantıklarını ve teknik endpoint detaylarını içerir.

---

## 🧭 1. Temel Çalışma Prensipleri

1. **Tek Doğruluk Kaynağı (Single Source of Truth):**
   - Talebin ana sahibi ve tek doğruluk kaynağı her zaman `destek.cmit.com.tr` portalıdır.
   - Faaliyet Yönetim Sistemi, talepleri kendi tarafında yönetmek için bizim açtığımız API uçlarına okuma (PULL) ve durum/yorum güncelleme (WRITE-BACK) istekleri atar.
2. **Kimlik Doğrulama:**
   - Tüm uç noktaları HTTPS protokolü ve `Authorization: Bearer <TOKEN>` veya `X-Api-Key: <TOKEN>` başlığı ile korunur.
   - İsteği yapan temsilcinin tespiti için `X-User-Email: temsilci.eposta@...` başlığı kullanılır.
3. **İdempotentlik:**
   - Durum güncelleme istekleri idempotenttir. Karşı sistem aynı durum güncellemesini yanlışlıkla birden fazla kez POST etse bile sistem mükerrer e-posta fırlatmaz, `200 OK` döner.

---

## 🛠️ 2. Endpointler ve Kullanım Detayları

### 1. Talep Listesi Çekme (GET /api/talepler)
- **Amaç:** Son güncellenen talepleri liste halinde çekmek (PULL).
- **HTTP Metodu:** `GET`
- **URL:** `https://destek.cmit.com.tr/api/talepler?updatedSince=2026-07-28T00:00:00Z&group=Sistem%20ve%20Altyap%C4%B1%20Operasyon`
- **Başlıklar:**
  ```http
  Authorization: Bearer <INTEGRATION_TOKEN>
  ```

---

### 2. Talep Detayı, Yorumlar ve Dosyalar (GET /api/talepler/{talepNo})
- **Amaç:** Bir talebin detay verilerini, kararlı ID'ye (`c-{id}`) sahip tüm yorumlarını ve ek dosyalarını çekmek.
- **HTTP Metodu:** `GET`
- **URL:** `https://destek.cmit.com.tr/api/talepler/26070052`
- **Örnek Yanıt:**
  ```json
  {
    "externalRef": "26070052",
    "url": "https://destek.cmit.com.tr/tickets/26070052/",
    "title": "Veeam backup sunucu arızası",
    "description": "Son alınan canlı ortam datasının backup'ı alınamadı...",
    "requesterName": "Ahmet Çöpüroğlu",
    "requesterEmail": "ahmet.copuroglu@tdv.org",
    "assigneeEmail": "aykut.ayvaz@cmit.com.tr",
    "group": "Sistem ve Altyapı Operasyon",
    "category": "TEKNİK DESTEK",
    "problemType": "Sistem, Donanım, Altyapı",
    "priority": "Orta",
    "status": "İşlemde",
    "createdAt": "2026-07-28T10:37:00+03:00",
    "updatedAt": "2026-07-28T11:20:00+03:00",
    "dueDate": "2026-07-28T17:00:00+03:00",
    "resolvedAt": null,
    "closedAt": null,
    "comments": [
      {
        "id": "c-1001",
        "author": "Aykut Ayvaz",
        "authorEmail": "aykut.ayvaz@cmit.com.tr",
        "date": "2026-07-28T11:20:00+03:00",
        "body": "Backup job oluşturuldu, test ediliyor.",
        "isInternal": false
      }
    ],
    "attachments": [
      {
        "id": "f-55",
        "name": "backup-config.png",
        "url": "https://destek.cmit.com.tr/api/talepler/26070052/dosya/f-55",
        "sizeBytes": 84213,
        "contentType": "image/png",
        "uploadedAt": "2026-07-28T11:22:00+03:00"
      }
    ]
  }
  ```

---

### 3. Durum Güncelleme (POST /api/talepler/{talepNo}/durum)
- **Amaç:** Faaliyet Yönetim Sistemi'nde durum değiştiğinde bunu `destek.cmit.com.tr` portalına işlemek.
- **HTTP Metodu:** `POST`
- **URL:** `https://destek.cmit.com.tr/api/talepler/26070052/durum`
- **Başlıklar:**
  ```http
  Authorization: Bearer <INTEGRATION_TOKEN>
  X-User-Email: aykut.ayvaz@cmit.com.tr
  Content-Type: application/json
  ```
- **İstek Gövdesi:**
  ```json
  {
    "status": "Çözüldü",
    "note": "Backup alındı, sorun giderildi. (Faaliyet Yönetim Sistemi'nden)"
  }
  ```
- **Başarılı Yanıt (200 OK):**
  ```json
  {
    "externalRef": "26070052",
    "status": "Çözüldü",
    "updatedAt": "2026-07-28T15:00:00+03:00"
  }
  ```

---

### 4. Yorum Ekleme (POST /api/talepler/{talepNo}/yorumlar)
- **Amaç:** Faaliyet Yönetim Sistemi'nde yazılan yorumu bizim sisteme aktarmak.
- **HTTP Metodu:** `POST`
- **URL:** `https://destek.cmit.com.tr/api/talepler/26070052/yorumlar`
- **Başlıklar:**
  ```http
  Authorization: Bearer <INTEGRATION_TOKEN>
  X-User-Email: aykut.ayvaz@cmit.com.tr
  Content-Type: application/json
  ```
- **İstek Gövdesi:**
  ```json
  {
    "body": "Sunucu yeniden başlatıldı, testler başarılı.",
    "isInternal": false
  }
  ```
- **Başarılı Yanıt (201 Created):**
  ```json
  {
    "id": "c-1042",
    "externalRef": "26070052",
    "createdAt": "2026-07-28T15:05:00+03:00"
  }
  ```

---

### 5. Ek Dosya İndirme (GET /api/talepler/{talepNo}/dosya/{dosyaId})
- **Amaç:** Aynı API token ile talebe eklenmiş dosyayı güvenli biçimde indirmek.
- **HTTP Metodu:** `GET`
- **URL:** `https://destek.cmit.com.tr/api/talepler/26070052/dosya/f-55`
- **Başlıklar:**
  ```http
  Authorization: Bearer <INTEGRATION_TOKEN>
  ```
- **Yanıt:** İlgili dosyanın ikili (binary) verisi ve `Content-Disposition` dosyası.

---

## 🔔 3. Arka Plan Bildirim, E-posta ve Otomasyon Süreçleri

API üzerinden gelen tüm işlemler, `notifications/services.py` ve `tickets/services.py` servis katmanlarımız tarafından doğrudan işlendiği için arayüzden yapılan işlemlerle birebir aynı otomasyonları tetikler:

### A. Durum Değişikliklerinde:
1. **Müşteriye (Son Kullanıcıya) E-posta:** Talebin durumu `İşlemde`, `Beklemede` veya `Çözüldü` olarak değiştiğinde müşteriye durum bilgilendirme e-postası gider.
2. **Çözüldü Durumu & Memnuniyet Anketi:** Durum `Çözüldü` (`resolved`) yapıldığında, müşteriye giden e-postada çözüm notu ile birlikte **Müşteri Memnuniyeti Anketi** bağlantısı otomatik eklenir.
3. **SLA Yönetimi:**
   - Durum `Beklemede` yapıldığında SLA duraklatılır (`sla_paused = True`).
   - Durum `İşlemde` veya `Açık` yapıldığında SLA kaldığı yerden devam eder (`due_date` yeniden hesaplanır).
4. **Denetim Kaydı (TicketActivity):** Talebin detay ekranındaki hareket geçmişine *"Durum 'İşlemde' -> 'Çözüldü' olarak değiştirildi (Faaliyet Yönetim Sistemi)."* kaydı düşer.

### B. Yorum Eklendiğinde:
1. **Genel Yorum (`isInternal: false`):** Müşteriye hem uygulama içi hem de *"Talebinize Yeni Yanıt Eklendi"* e-postası iletilir.
2. **Dahili Not (`isInternal: true`):** Müşteriye bildirim gitmez, sadece temsilciler ve yöneticiler görebilir.
3. **XSS Güvenliği:** Yorum metinleri sunucu tarafında `sanitize_comment` süzgecinden geçirilerek saklanır.

### C. Admin & Supervisor Kuralı (GEMINI.md Anayasası):
- Admin ve Supervisor rollerine e-posta gönderilmez; bu kullanıcılar sistemi uygulama içi çan bildirimleriyle canlı olarak takip eder.

---

## 🧪 4. Test ve Doğrulama

Birim ve entegrasyon testlerini çalıştırmak için:

```bash
source /home/hasanozcan/.test_cmit_venv/bin/activate
python manage.py test tickets.tests_integration_api --settings=support_system.settings.test
```

Çıktı:
```text
Ran 11 tests in 0.316s — OK
```
