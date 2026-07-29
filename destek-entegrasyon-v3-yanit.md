# Destek Sistemi Entegrasyonu — V3 Teknik Yanıt Dokümanı
### (Yoruma Dosya Ekleme / Write-Back Upload)

**Gönderen:** destek.cmit.com.tr (Cortex Suite) Geliştirme Ekibi  
**Alıcı:** TDV/CMIT Altyapı Ekibi — Faaliyet Yönetim Sistemi (`activitymanagement.tdv.org`)  
**Konu:** V3 Entegrasyon İstek Dokümanı (Yoruma Dosya Ekleme) Teknik Yanıtı ve Servis Tanımları  
**Tarih:** 28 Temmuz 2026  
**Durum:** Geliştirme Tamamlandı & Kullanıma Hazır  

---

## 1. Genel Bilgilendirme

V3 entegrasyon istek dokümanınız tarafımızca incelenmiş ve önerdiğiniz **Seçenek A (Yorum + Çoklu Dosya Yükleme)** mimarisi aynen kabul edilerek `destek.cmit.com.tr` portalımızda aktif edilmiştir.

Bu geliştirme ile temsilcileriniz `activitymanagement.tdv.org` üzerinden bir talebe yanıt yazarken veya ekran görüntüsü (Ctrl+V) / dosya eklediğinde, tek istek içerisinde hem yorumu hem de dosyaları tarafımıza aktarabilecektir.

---

## 2. Kimlik Doğrulama & Yetkilendirme

V2 entegrasyonu ile aynı güvenlik standartları geçerlidir:

* **Header:** `Authorization: Bearer <INTEGRATION_TOKEN>` *(veya `X-Api-Key: <INTEGRATION_TOKEN>`)*
* **Header:** `X-User-Email: temsilci.eposta@...` *(İşlemi gerçekleştiren temsilci)*
* **Grup Kısıtı:** Yalnızca `Sistem ve Altyapı Operasyon` grubuna atanmış talepler için dosya/yorum yüklemesi kabul edilir. Başka bir gruba ait talebe yükleme yapıldığında `403 Forbidden` yanıtı döner.

---

## 3. Uç Nokta (Endpoint) Tanımı — Seçenek A

Mevcut `POST /api/talepler/{talepNo}/yorumlar` uç noktamız hem `application/json` (sadece metin) hem de `multipart/form-data` (metin + dosya) kabul edecek şekilde genişletilmiştir.

```http
POST https://destek.cmit.com.tr/api/talepler/{talepNo}/yorumlar
Content-Type: multipart/form-data
Authorization: Bearer <INTEGRATION_TOKEN>
X-User-Email: temsilci.eposta@cmit.com.tr
```

### Form Alanları (Multipart/Form-Data)

| Alan Adı | Veri Tipi | Zorunlu mu? | Açıklama |
| :--- | :--- | :---: | :--- |
| `body` | String (metin) | Hayır* | Yorum metni (*En az bir dosya yüklendiğinde metin alanı boş bırakılabilir). |
| `isInternal` | Boolean / String | Hayır | `true` veya `false` (Varsayılan: `false`). `true` gönderilirse yorum ve yüklenen tüm ekler **dahili not** olarak işaretlenir (müşteriye kapalı tutulur). |
| `files` | File (çoklu) | Hayır* | Yüklenecek görsel veya dosyalar. Parametre adı `files` (veya `file`) olarak çoklu dosya biçiminde gönderilebilir. |

---

## 4. Yanıt Formatı (Response Specification)

### Başarılı Yanıt (`HTTP 201 Created`)

Oluşturulan yorumun bilgisi ve yüklenen eklerin kararlı `id` ve `url` değerleri V2 `attachments[]` şemasıyla birebir aynı formatta döner:

```json
{
  "id": "c-1055",
  "externalRef": "26070300",
  "createdAt": "2026-07-28T16:48:00+03:00",
  "attachments": [
    {
      "id": "f-9001",
      "name": "ekran-goruntusu.png",
      "url": "https://destek.cmit.com.tr/api/talepler/26070300/dosya/f-9001",
      "sizeBytes": 84213,
      "contentType": "image/png",
      "uploadedAt": "2026-07-28T16:48:00+03:00"
    }
  ]
}
```

---

## 5. Dokümandaki Açık Sorulara Yanıtlar (§6)

1. **A mı B mi tercih edildi?**  
   👉 **Seçenek A** tercih edildi. `POST /api/talepler/{talepNo}/yorumlar` uç noktasına `multipart/form-data` ile aynı anda hem yorum metni hem de `files` yüklenebilir.
2. **Azami dosya boyutu ve izinli türler nelerdir?**  
   👉 Dosya başı azami boyut **25 MB**'tır (aşımda `413 Payload Too Large`). İzin verilen türler: Görseller (`png, jpg, jpeg, gif, webp`), Dokümanlar (`pdf, doc, docx, xls, xlsx, txt, log, zip, rar`). Güvenlik nedeniyle çalıştırılabilir dosyalar (`.exe, .sh, .php` vb.) engellenir (`415 Unsupported Media Type`).
3. **Dönen `id` ve `url` V2 ile aynı mı?**  
   👉 Evet, dönen `f-{id}` ve `url` değerleri V2 detay (`GET .../{no}` → `attachments[]`) ve dosya indirme (`GET .../dosya/{id}`) uçları ile %100 aynıdır. Dönen `id` bilgisini dedup (mükerrer kayıt engelleme) için güvenle kullanabilirsiniz.
4. **`isInternal` görünürlük kuralı?**  
   👉 `isInternal=true` gönderildiğinde yorum ve eklenen dosyalar yalnızca temsilci ve yöneticilere açık, müşteriye kapalı tutulur.
5. **HTTP Hata Kodları:**  
   * `400 Bad Request`: Hem `body` hem de `files` boş gönderildiğinde.
   * `401 Unauthorized`: API Token eksik veya geçersizse.
   * `403 Forbidden`: Talep `Sistem ve Altyapı Operasyon` grubuna ait değilse.
   * `404 Not Found`: Belirtilen talep numarası sistemde bulunamadığında.
   * `413 Payload Too Large`: Dosya boyutu 25 MB sınırını aştığında.
   * `415 Unsupported Media Type`: Desteklenmeyen / tehlikeli dosya türü yüklendiğinde.

---

## 6. Örnek İstekler (Code Snippets)

### cURL Örneği (Yorum + Görsel Yükleme)

```bash
curl -X POST "https://destek.cmit.com.tr/api/talepler/26070300/yorumlar" \
  -H "Authorization: Bearer <INTEGRATION_TOKEN>" \
  -H "X-User-Email: aykut.ayvaz@cmit.com.tr" \
  -F "body=Ekran görüntüsü incelenebilir." \
  -F "isInternal=true" \
  -F "files=@/path/to/ekran-goruntusu.png"
```

### Python Örneği (`requests` kütüphanesi)

```python
import requests

url = "https://destek.cmit.com.tr/api/talepler/26070300/yorumlar"
headers = {
    "Authorization": "Bearer YOUR_INTEGRATION_TOKEN",
    "X-User-Email": "aykut.ayvaz@cmit.com.tr"
}

data = {
    "body": "Sorun giderildi, ekran çıktısı ektedir.",
    "isInternal": "false"
}

files = [
    ('files', ('ekran1.png', open('ekran1.png', 'rb'), 'image/png'))
]

response = requests.post(url, headers=headers, data=data, files=files)
print(response.status_code, response.json())
```

---

Geliştirmeler canlı/test ortamına aktarılmıştır. Entegrasyon istemcinizi bu uç noktaya bağlayarak test edebilirsiniz.
