# Destek V3 — Hata Bildirimi (Yoruma Dosya Ekleme / multipart)

**Gönderen:** TDV/CMIT Altyapı Ekibi — Faaliyet Yönetim Sistemi (activitymanagement.tdv.org)
**Muhatap:** destek.cmit.com.tr (Cortex Suite) geliştirme ekibi
**Konu:** V3 yanıt dokümanınızda "tamamlandı" denen **multipart/form-data (yorum + dosya)** ucu canlıda **multipart isteğini reddediyor** — Content-Type'a bakmadan JSON parse ediyor.
**Öncelik:** Orta — bizim taraf hazır, yalnız bu uç düzelince dosya/ekran görüntüsü gönderimi çalışacak.

---

## 1. Özet

`POST /api/talepler/{talepNo}/yorumlar` ucu:
- **`application/json`** ile (yalnız metin) → **çalışıyor** (`201 Created`).
- **`multipart/form-data`** ile (metin + `files`) → **çalışmıyor**: `400 Bad Request` + `{"error": "Geçersiz veya bozuk JSON gövdesi."}`

Hata mesajı ("JSON gövdesi") gösteriyor ki uç, gelen isteğin **Content-Type'ını dikkate almadan** her durumda gövdeyi JSON olarak ayrıştırmaya çalışıyor. Multipart dalı devreye girmiyor.

---

## 2. Kanıt (aynı talep, aynı token, ardışık iki istek)

**A) JSON — BAŞARILI**
```
POST /api/talepler/26070302/yorumlar
Content-Type: application/json; charset=utf-8
Authorization: Bearer <token>
X-User-Email: aykut.ayvaz@cmit.com.tr

{"body":"...","isInternal":true}
```
Yanıt:
```
HTTP 201
{"id":"c-113131","externalRef":"26070302","createdAt":"2026-07-28T21:06:50+03:00"}
```

**B) MULTIPART — BAŞARISIZ**
```
POST /api/talepler/26070302/yorumlar
Content-Type: multipart/form-data; boundary="15fff545-256d-4855-8940-099173e16e7f"
Authorization: Bearer <token>
X-User-Email: aykut.ayvaz@cmit.com.tr

--boundary
Content-Disposition: form-data; name="body"

[metin]
--boundary
Content-Disposition: form-data; name="isInternal"

true
--boundary
Content-Disposition: form-data; name="files"; filename="cmp.txt"
Content-Type: text/plain

[dosya içeriği]
--boundary--
```
Yanıt:
```
HTTP 400
{"error": "Geçersiz veya bozuk JSON gövdesi."}
```

> Aynı istek **cURL `-F`** (sizin v3-yanıt §6'daki örneğin birebir aynısı) ile de aynı `400`'ü veriyor. Yani sorun istemci kütüphanesinde değil; her standart multipart isteği reddediliyor.

---

## 3. Beklenen davranış (v3-yanıt §3 ile aynı)

Uç, `Content-Type` başlığına göre dallanmalı:
- `application/json` → mevcut davranış (yalnız `body`, `isInternal`).
- `multipart/form-data` → form alanları `body` (opsiyonel), `isInternal` (opsiyonel), `files` (çoklu dosya) okunmalı; `201` + `attachments[]` dönmeli.

Muhtemel kök neden: request pipeline'ında **her istekte JSON body okuyan** bir ara katman/parser var; multipart isteklerde bu katman atlanmalı (veya Content-Type multipart ise JSON parse denenmemeli).

---

## 4. Bizim tarafın durumu

- İstemcimiz **hazır**: dosya varsa `multipart/form-data` (alanlar `body` + `isInternal` + `files`), yoksa `application/json` gönderiyor; Content-Type + boundary standart.
- Dönen `attachments[]` (id/url) ile eki saklayıp gösteriyoruz; sizin `GET .../dosya/{id}` ile indiriyoruz (bunlar V2'de çalışıyor).
- Bu uç düzelir düzelmez ek gönderimi (Ctrl+V ekran görüntüsü dâhil) çalışacaktır; ek bir geliştirme gerekmiyor.

---

## 5. Ricamız

`POST /api/talepler/{talepNo}/yorumlar` ucunun **multipart/form-data** dalını canlıya alıp doğrular mısınız? Doğruladıktan sonra tekrar test edip bağlantıyı kapatırız. Teşekkürler.
