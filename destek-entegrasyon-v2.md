# Destek Sistemi Entegrasyon İstek Dokümanı — V2 (Detay + Çift Yönlü)
### destek.cmit.com.tr (CMIT Bilişim – Destek Sistemi) ⇄ Faaliyet Yönetim Sistemi

**Hazırlayan:** TDV/CMIT Altyapı Ekibi — Faaliyet Yönetim Sistemi (activitymanagement.tdv.org)
**Muhatap:** destek.cmit.com.tr (Cortex Suite) yönetim/geliştirme ekibi
**Konu:** V1'deki liste (PULL) entegrasyonunun üzerine **talep detayı (yorum + dosya + durum)** çekme ve **çift yönlü** (bizden size **durum güncelleme + yorum ekleme**) desteği.
**Ön koşul:** V1 liste ucu (`GET /api/talepler`) hâlihazırda çalışıyor. Bu doküman onun **üzerine** ek 3 uç ister.

---

## 1. Amaç

Altyapı ekibimiz talepleri Faaliyet Yönetim Sistemi'nde yönetiyor. Şimdi iki şey istiyoruz:

1. **Detay çekme (okuma):** Bir talebin **yorumlarını, dosya eklerini ve güncel durumunu** çekmek. (V1 liste ucu yalnız meta veriyor; yorum/dosya yok.)
2. **Çift yönlü (yazma / write-back):** Temsilcimiz **bizim** sistemde talebi **çözüldü/kapandı** yaptığında veya **yorum girdiğinde**, bunun **sizin** tarafınıza da işlenmesi. Böylece iki sistem tutarlı kalır ve tek doğruluk kaynağı yine sizin sisteminizdir.

**Model (önemli):** Durumun/​yorumun **tek sahibi sizsiniz**. Biz durumu kendi tarafımızda "komut" olarak size POST'larız; siz uygularsınız; biz bir sonraki okumada güncel hâli sizden çekeriz. Böylece **çakışma olmaz**.

---

## 2. Kimlik doğrulama

Üç uç da **V1 liste ucuyla AYNI kimlik doğrulamasını** kullanır (aynı API anahtarı/başlığı, HTTPS zorunlu). Ek bir anahtar gerekmez.

İsteğe bağlı ama tercih edilir: işlemi yapan **temsilcinin e-postasını** ayrı bir başlıkta gönderelim ki "kim güncelledi / kim yorum yazdı" sizde doğru görünsün:
```
X-User-Email: aykut.ayvaz@...    (bizim temsilcinin e-postası)
```
> Not: Bizdeki e-posta alan adı farklı olabilir (ör. `@cmit.com.tr`), sizde `@tdv.org`. Eşleştirmeyi **@ öncesi ön-ek** (ör. `aykut.ayvaz`) ile yapmanız yeterli; ya da ortak bir servis hesabı tanımlayabiliriz.

---

## 3. Endpoint 1 — Talep Detayı (GET) — yorum + dosya + durum

```
GET https://destek.cmit.com.tr/api/talepler/{talepNo}
```
`{talepNo}` = V1'deki `externalRef` (ör. `26070052`).

### 3.1. Yanıt (JSON) — örnek
```json
{
  "externalRef": "26070052",
  "url": "https://destek.cmit.com.tr/talep/26070052",
  "title": "Veeam backup",
  "description": "Son alınan canlı ortam datasının backup'ı...",
  "requesterName": "Ahmet Çöpüroğlu",
  "requesterEmail": "ahmet.copuroglu@tdv.org",
  "assigneeEmail": "aykut.ayvaz@cmit.com.tr",
  "group": "Sistem ve Altyapı Operasyon",
  "category": "TEKNİK DESTEK",
  "priority": "Orta",
  "status": "Çözüldü",
  "createdAt": "2026-07-03T10:37:00+03:00",
  "updatedAt": "2026-07-05T14:00:00+03:00",
  "resolvedAt": "2026-07-05T09:00:00+03:00",
  "closedAt": "2026-07-05T14:00:00+03:00",

  "comments": [
    {
      "id": "c-1001",
      "author": "Aykut Ayvaz",
      "authorEmail": "aykut.ayvaz@cmit.com.tr",
      "date": "2026-07-04T11:20:00+03:00",
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
      "uploadedAt": "2026-07-04T11:22:00+03:00"
    }
  ]
}
```

### 3.2. Alan notları
- **comments[]** (ZORUNLU — bu dokümanın ana isteği): her yorum kararlı bir **`id`** taşımalı (tekrar çekince kopyalamayalım diye). `body` düz metin ya da HTML olabilir; `isInternal` ile dahili/müşteriye-kapalı notu ayırt edin.
- **attachments[]**: dosya listesi + **indirme `url`si** (aynı kimlikle indirilebilir olmalı). İçeriği base64 gömmeye gerek yok; URL yeterli.
- **status**: V1'deki durum değerleriyle aynı sözlük (bkz. §6).
- **updatedAt**: yorum/dosya/durum değişiminde güncellensin (artımlı senkronu besler).

---

## 4. Endpoint 2 — Durum Güncelleme (POST / write-back)

Temsilcimiz bizde talebi çözdü/kapattığında size iletiriz.
```
POST https://destek.cmit.com.tr/api/talepler/{talepNo}/durum
Content-Type: application/json
X-User-Email: aykut.ayvaz@...        (opsiyonel — işlemi yapan)
```
### 4.1. İstek gövdesi
```json
{
  "status": "Çözüldü",
  "note": "Backup alındı, doğrulandı. (Faaliyet Yönetim Sistemi'nden)"
}
```
- `status`: §6 tablosundaki değerlerden biri.
- `note` (opsiyonel): durum değişikliğiyle birlikte otomatik bir sistem yorumu.

### 4.2. Yanıt
```json
{ "externalRef": "26070052", "status": "Çözüldü", "updatedAt": "2026-07-05T09:00:00+03:00" }
```
- Başarı: `200`. Bilinmeyen talep: `404`. Geçersiz durum/izin: `409/422`. Yetkisiz: `401`.
- **İdempotent:** aynı durumu tekrar POST'larsak hata vermeyin (200 dönün).

---

## 5. Endpoint 3 — Yorum Ekleme (POST / write-back)

Temsilcimiz bizde yorum girince size push ederiz.
```
POST https://destek.cmit.com.tr/api/talepler/{talepNo}/yorumlar
Content-Type: application/json
X-User-Email: aykut.ayvaz@...        (opsiyonel — yorumu yazan)
```
### 5.1. İstek gövdesi
```json
{
  "body": "Sunucu yeniden başlatıldı, sorun giderildi.",
  "isInternal": false
}
```
### 5.2. Yanıt
```json
{ "id": "c-1042", "externalRef": "26070052", "createdAt": "2026-07-05T09:05:00+03:00" }
```
- Başarı: `201` + oluşturulan yorumun **`id`si** (biz bunu saklayıp, bir sonraki detay çekiminde kopyalamayız).
- **Echo/kopya önleme:** Bizim eklediğimiz yorumu §3 detayında geri döndürseniz de sorun değil; `id` ile ayırırız.

---

## 6. Durum değerleri eşlemesi (bizim ↔ sizin)

Aşağıdaki değerleri hem okuma (§3) hem yazma (§4) için ortak kullanalım. Sizdeki karşılıkları farklıysa lütfen **tam listeyi** paylaşın; biz eşleriz.

| Bizim durum | Beklenen `status` metni (sizde) |
|---|---|
| Yeni | Yeni / Açık / New |
| Atandı | Atandı / Assigned |
| Devam Ediyor | Devam Ediyor / İşlemde / In Progress |
| Beklemede | Beklemede / Pending / Hold |
| Çözüldü | Çözüldü / Resolved |
| Kapandı | Kapatıldı / Kapandı / Closed |
| İptal | İptal / Cancelled |

---

## 7. Güvenlik / kurallar

- Tüm uçlar **HTTPS** ve **API anahtarı** ile (V1 ile aynı). Anahtar başlıkta gönderilir, gövdede/URL'de değil.
- **Grup kapsamı:** Yazma uçları da yalnız bizim grubumuza (`Sistem ve Altyapı Operasyon`) ait taleplerde çalışsın; başka grup talebine yazma reddedilsin (403).
- **İdempotent** davranış (§4.2, §5.2). Zaman damgaları **ISO 8601** (+03:00).
- Hata durumunda anlamlı HTTP kodu + `{ "error": "..." }` gövdesi.

---

## 8. Sizden ricamız (özet) ve açık sorular

**Açmanızı istediğimiz 3 uç:**
1. `GET /api/talepler/{talepNo}` → **yorumlar + dosyalar + durum** (§3).
2. `POST /api/talepler/{talepNo}/durum` → durum güncelleme (§4).
3. `POST /api/talepler/{talepNo}/yorumlar` → yorum ekleme (§5).

**Netleştirmemiz gereken sorular:**
1. Bu 3 uç Cortex Suite'te açılabilir mi? Açılabilirse yol/method/gövde bizim önerimizle mi olur, sizde farklı bir kalıp mı var?
2. **Durum değerlerinizin tam listesi** nedir (§6 eşlemesi için)?
3. **Yorum** ve **dosya** verisi API'de dönebiliyor mu? Dosya için indirme URL'si aynı API anahtarıyla erişilebilir mi?
4. Yazarken **aktör** (kim güncelledi/yorumladı) nasıl belirtilsin — `X-User-Email` uygun mu, yoksa ortak servis hesabı mı?
5. Yazma uçlarında grup/yetki kısıtı nasıl olmalı?

Dönüşünüze göre bizim taraftaki entegrasyonu (detay senkronu + write-back) hızlıca tamamlayabiliriz. Teşekkürler.
