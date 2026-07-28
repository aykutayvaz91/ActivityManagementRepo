# Sunucu Kurulum Talepleri Entegrasyon İstek Dokümanı — V2 (Detay + Çift Yönlü)
### psm.tdv.org (Sunucu Kurulum/Provizyon Sistemi) ⇄ Faaliyet Yönetim Sistemi

**Hazırlayan:** TDV/CMIT Altyapı Ekibi — Faaliyet Yönetim Sistemi (activitymanagement.tdv.org)
**Muhatap:** psm.tdv.org geliştirme/yönetim ekibi
**Konu:** V1'deki liste (PULL) entegrasyonunun üzerine **talep detayı (yorum + dosya + güncel durum + kurulum künyesi)** çekme ve **çift yönlü** (bizden size **durum/aksiyon + yorum** iletme) desteği.
**Ön koşul:** V1 liste ucu (`GET /api/kurulum-talepleri`) hâlihazırda **çalışıyor** (API anahtarı + `X-User-Email` başlığı ile). Bu doküman onun **üzerine** ek uçlar ister; mevcut liste ucu değişmez.

---

## 1. Amaç

Sunucu kurulum taleplerini Faaliyet Yönetim Sistemi'nde "iş kartı" olarak yönetiyor, üzerine **efor** giriyoruz. V1 liste ucu talebin meta verisini + kurulum künyesini getiriyor; ancak **yorumlar, dosya ekleri ve talep zaman çizelgesindeki güncel durum** gelmiyor. Şimdi iki şey istiyoruz:

1. **Detay çekme (okuma):** Bir talebin **yorumlarını / notlarını, dosya eklerini, güncel durumunu** ve (varsa güncellenmiş) **kurulum künyesini** tek uçtan çekmek.
2. **Çift yönlü (yazma / write-back):** Kurulumu yapan uzmanımız **bizim** sistemde işi **tamamladığında / reddettiğinde** veya **not/yorum girdiğinde** bunun **sizin** tarafınıza da işlenmesi. Böylece iki sistem tutarlı kalır.

**Model (önemli — çakışma önleme):** Talebin durumunun **tek sahibi sizsiniz (psm)**. Biz durumu kendi tarafımızda "komut/aksiyon" olarak size iletiriz; siz uygularsınız; biz bir sonraki okumada güncel hâli **yine sizden çekeriz**. Yerelde durumu biz değiştirmeyiz — her zaman psm'i doğru kabul ederiz.

---

## 2. Kimlik doğrulama

Tüm yeni uçlar **V1 liste ucuyla AYNI kimlik doğrulamasını** kullanır — ek anahtar gerekmez:

```
{ApiKeyHeader}: {API anahtarı}        (V1 ile aynı)
X-User-Email: aykut.ayvaz@tdv.org     (işlemi yapan/isteği atan uzmanın e-postası — V1'de de kullanılıyor)
```

- HTTPS zorunlu; anahtar **başlıkta** gönderilir (gövdede/URL'de değil).
- **E-posta eşleştirme:** Bizdeki alan adı farklı olabilir (ör. `@cmit.com.tr`), sizde `@tdv.org`. Eşleştirmeyi **`@` öncesi ön-ek** (ör. `aykut.ayvaz`) ile yapıyoruz; write-back'te `X-User-Email` ile "kim yaptı" bilgisini iletebiliriz.

---

## 3. Endpoint 1 — Talep Detayı (GET) — yorum + dosya + durum + kurulum künyesi

```
GET https://psm.tdv.org/api/kurulum-talepleri/{talepNo}
{ApiKeyHeader}: {API anahtarı}
X-User-Email: aykut.ayvaz@tdv.org
```
`{talepNo}` = V1'deki `externalRef` (ör. `PSM-2026-0142`).

### 3.1. Yanıt (JSON) — örnek (PAROLASIZ)
```json
{
  "externalRef": "PSM-2026-0142",
  "url": "https://psm.tdv.org/talep/142",
  "title": "On-prem container registry için vm talebi",
  "description": "On-prem CI/CD altyapısı ve container registry için iki adet Ubuntu Server VM talep ediyoruz.",

  "requesterName": "Mustafa Can",
  "requesterUnit": "Yazılım Birimi",
  "requesterEmail": "mustafa.can@tdv.org",
  "assignedByEmail": "mustafa.keser@cmit.com.tr",
  "assigneeEmail": "aykut.ayvaz@cmit.com.tr",

  "priority": "Normal",
  "network": "Statik IP",
  "status": "Kurulumda",
  "createdAt": "2026-07-24T09:00:00+03:00",
  "updatedAt": "2026-07-24T09:53:00+03:00",
  "startedAt": "2026-07-24T09:00:00+03:00",
  "completedAt": null,

  "comments": [
    {
      "id": "n-3001",
      "author": "Aykut Ayvaz",
      "authorEmail": "aykut.ayvaz@cmit.com.tr",
      "date": "2026-07-24T09:40:00+03:00",
      "body": "VM oluşturuldu, OS kurulumu başladı.",
      "isInternal": false
    }
  ],

  "attachments": [
    {
      "id": "d-77",
      "name": "kurulum-formu.pdf",
      "url": "https://psm.tdv.org/api/kurulum-talepleri/PSM-2026-0142/dosya/d-77",
      "sizeBytes": 152340,
      "contentType": "application/pdf",
      "uploadedAt": "2026-07-24T09:41:00+03:00"
    }
  ],

  "requested": {
    "os": "Ubuntu Server 24.04 LTS", "type": "Fiziksel",
    "cpu": 8, "ramGb": 16, "disk1Gb": 100, "disk2Gb": null,
    "env": "Prod", "location": null
  },
  "installed": {
    "hostname": "tdvcicd2tr1", "os": "Ubuntu Server 24.04 LTS", "type": "Fiziksel",
    "cpu": 8, "ramGb": 16, "disk1Gb": 100, "disk2Gb": null, "env": "Prod",
    "ip": "192.168.152.142", "iloIp": null, "dns": null, "mac": null,
    "location": null, "notes": null
  },
  "services": [
    { "name": "SSH", "port": 22, "protocol": "TCP", "open": true }
  ]
}
```

### 3.2. Alan notları
- **comments[]** (bu dokümanın ana isteği): her yorum/not kararlı bir **`id`** taşımalı (tekrar çekince kopyalamayalım diye). `body` düz metin veya HTML olabilir; `isInternal` ile dahili notu ayırt edin.
- **attachments[]**: dosya listesi + **indirme `url`si** (aynı API anahtarı + `X-User-Email` ile erişilebilir olmalı). İçeriği base64 gömmeye gerek yok; URL yeterli. **Parola/kimlik içeren dosyalar gönderilmez** (§7).
- **status**: V1'deki değerlerle aynı sözlük (bkz. §6).
- **installed / services**: kurulum ilerledikçe güncellenen künye; detay çekiminde **en güncel** hâliyle dönsün (talebin "Ek Bilgi/kurulum" verisi olarak saklanır).
- **updatedAt**: yorum/dosya/durum/künye değişiminde güncellensin (artımlı senkronu besler).

---

## 4. Endpoint 2 — Durum/Aksiyon (POST / write-back)

psm akışı **aksiyon tabanlı** (Kurulumda → Tamamla / Reddet). Bizde uzman işi bitirince veya iptal edince size iletiriz. İki yaklaşımdan **sizde mevcut olanı** kullanırız:

### 4.a) Aksiyon uçları (psm'de zaten varsa — TERCİH)
```
POST https://psm.tdv.org/api/kurulum-talepleri/{talepNo}/tamamla
POST https://psm.tdv.org/api/kurulum-talepleri/{talepNo}/reddet
Content-Type: application/json
{ApiKeyHeader}: {API anahtarı}
X-User-Email: aykut.ayvaz@tdv.org
```
Gövde (opsiyonel not + tamamlanan künye):
```json
{
  "note": "Kurulum tamamlandı, SSH açık, teslim edildi. (Faaliyet Yönetim Sistemi'nden)",
  "installed": { "hostname": "tdvcicd2tr1", "ip": "192.168.152.142" }
}
```

### 4.b) Tek durum ucu (alternatif)
```
POST https://psm.tdv.org/api/kurulum-talepleri/{talepNo}/durum
```
```json
{ "status": "Tamamlandı", "note": "..." }
```

### 4.1. Yanıt
```json
{ "externalRef": "PSM-2026-0142", "status": "Tamamlandı", "updatedAt": "2026-07-24T09:53:00+03:00" }
```
- Başarı: `200`. Bilinmeyen talep: `404`. Geçersiz durum/izin: `409/422`. Yetkisiz: `401`.
- **İdempotent:** aynı aksiyonu/durumu tekrar POST'larsak hata vermeyin (`200` dönün).

---

## 5. Endpoint 3 — Not / Yorum Ekleme (POST / write-back)

Uzmanımız bizde not girince size push ederiz.
```
POST https://psm.tdv.org/api/kurulum-talepleri/{talepNo}/notlar
Content-Type: application/json
{ApiKeyHeader}: {API anahtarı}
X-User-Email: aykut.ayvaz@tdv.org
```
### 5.1. İstek gövdesi
```json
{ "body": "Disk genişletildi, servis yeniden başlatıldı.", "isInternal": false }
```
### 5.2. Yanıt
```json
{ "id": "n-3042", "externalRef": "PSM-2026-0142", "createdAt": "2026-07-24T10:05:00+03:00" }
```
- Başarı: `201` + oluşturulan notun **`id`si** (biz saklayıp bir sonraki detay çekiminde kopyalamayız).
- **Echo/kopya önleme:** Bizim eklediğimiz notu §3 detayında geri döndürseniz de sorun değil; `id` ile ayırt ederiz.

---

## 6. Durum değerleri eşlemesi (bizim ↔ sizin)

Bizim durumlarımız: **Yeni · Atandı · Devam Ediyor · Beklemede · Çözüldü · Kapandı · İptal**

| Bizim durum | psm karşılığı (beklenen) |
|---|---|
| Yeni | Bekliyor / Yeni |
| Atandı | Atandı |
| Devam Ediyor | Kurulumda |
| Beklemede | Beklemede (varsa) |
| Çözüldü / Kapandı | Tamamlandı |
| İptal | İptal / Red |

**Tam durum listenizi** iletin; okuma (§3) ve yazma (§4) için eşlemeyi buna göre kesinleştiririz.

---

## 7. Güvenlik / kurallar (KRİTİK)

- **PAROLA VE KİMLİK BİLGİLERİ ASLA GÖNDERİLMEZ.** psm'deki **Admin Şifre/Kullanıcı, ILO Şifre/Kullanıcı** ve "Kayıtlı Şifreler" alanları hiçbir yanıtta (§3 dâhil) ve hiçbir dosyada (§3.2 attachments) yer almamalıdır.
- **Yalnız ilgili kayıtlar:** Yazma uçları da yalnız **bizim ekibimize atanan** taleplerde çalışsın; başka atanana ait talebe yazma reddedilsin (`403`).
- Tüm uçlar **HTTPS** + **V1 ile aynı API anahtarı**. Zaman damgaları **ISO 8601** (+03:00).
- **İdempotent** davranış (§4.1, §5.2). Hata durumunda anlamlı HTTP kodu + `{ "error": "..." }` gövdesi.
- **Ağ:** psm iç IP'li. Bizim sunucumuzdan psm API'sine (yeni uçlar dâhil) erişim açık olmalı.

---

## 8. Sizden ricamız (özet) ve açık sorular

**Açmanızı istediğimiz uçlar:**
1. `GET /api/kurulum-talepleri/{talepNo}` → **yorumlar + dosyalar + güncel durum + kurulum künyesi** (§3).
2. `POST .../tamamla` + `.../reddet` **veya** `.../durum` → durum/aksiyon write-back (§4).
3. `POST .../notlar` → not/yorum ekleme write-back (§5).

**Netleştirmemiz gereken sorular:**
1. Bu uçlar psm'de açılabilir mi? psm'de yazma için **hazır aksiyon uçları (tamamla/reddet)** var mı, yoksa tek **durum** ucu mu daha uygun?
2. **Tam durum listeniz** nedir (§6 için)? Özellikle iptal/red ayrımı.
3. Talebe ait **yorum/not** ve **dosya** verisi API'de dönebiliyor mu? Dosya indirme URL'si aynı API anahtarı + `X-User-Email` ile erişilebilir mi?
4. Yazarken **aktör** (kim yaptı) `X-User-Email` ile mi belirtilsin, yoksa ortak servis hesabı mı?
5. Kurulum künyesindeki (installed) hangi alanları tamamlama sırasında bizden **geri** almak istersiniz (§4.a `installed`)? Yoksa künye tümüyle sizde mi güncellenir?

Dönüşünüze göre bizim taraftaki entegrasyonu (detay senkronu + write-back) hızlıca tamamlarız. Bizim tarafta talep detayını **saklayıp gösterecek** altyapı (yorum + dosya aynası) **hazırdır**; yalnızca yukarıdaki uçlar açılınca bağlarız. Teşekkürler.
