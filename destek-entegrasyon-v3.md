# Destek Sistemi Entegrasyon İstek Dokümanı — V3 (Yoruma Dosya Ekleme / Write-Back Upload)
### destek.cmit.com.tr (CMIT Bilişim – Destek Sistemi) ⇄ Faaliyet Yönetim Sistemi

**Hazırlayan:** TDV/CMIT Altyapı Ekibi — Faaliyet Yönetim Sistemi (activitymanagement.tdv.org)
**Muhatap:** destek.cmit.com.tr (Cortex Suite) yönetim/geliştirme ekibi
**Konu:** V2'de eklenen **çift yönlü** (durum + yorum write-back) desteğinin üzerine, **bizden size dosya / ekran görüntüsü gönderme** (yoruma dosya ekleme) yeteneği.
**Ön koşul:** V2 uçları çalışıyor — liste (`GET /api/talepler`), detay (`GET /api/talepler/{no}` → yorumlar + dosyalar + durum), durum write-back (`POST .../durum`), yorum write-back (`POST .../yorumlar`), dosya **indirme** (`GET .../dosya/{id}`). Bu doküman onların üzerine **1 adet yeni yazma ucu** ister.

---

## 1. Amaç

V2 ile temsilcimiz sizin sistemdeki talebe **durum** ve **yorum** yazabiliyor. Ancak temsilci bir yoruma **dosya / ekran görüntüsü** eklemek istediğinde, bunu size iletecek bir uç **yok**: mevcut `.../dosya/{id}` yalnız **GET (indirme)**, `.../yorumlar` ise yalnız **metin gövdesi** (`body`) kabul ediyor.

İstediğimiz: temsilcimiz bir yoruma ekran görüntüsü/dosya eklediğinde bunun **sizin talebinize de eklenmesi** ve talebin **dosya listesinde** (V2 detay `attachments[]`) görünmesi. Böylece dosya iki sistemde de tutarlı olur; biz zaten V2 `attachments[]` + `GET .../dosya/{id}` ile okuyup gösterebiliyoruz — tek eksik **yükleme (write)** ucu.

**Model (V2 ile aynı):** Talebin ve eklerinin tek doğruluk kaynağı sizsiniz. Biz dosyayı size **yükleriz**; siz talebe eklersiniz; biz bir sonraki detay okumasında (`attachments[]`) güncel hâli sizden çekeriz.

---

## 2. Kimlik doğrulama

V2 yazma uçlarıyla **AYNI** kimlik — ek anahtar gerekmez:
```
Authorization: Bearer <INTEGRATION_TOKEN>        (veya V2'deki X-Api-Key)
X-User-Email: temsilci.eposta@...                (dosyayı yükleyen aktör)
```
HTTPS zorunlu. **Grup kısıtı:** yalnız bizim grubumuza (`Sistem ve Altyapı Operasyon`) ait taleplere yükleme kabul edin; başka grup talebine yükleme reddedilsin (`403`).

---

## 3. İstediğimiz uç — iki seçenek (sizde hangisi kolaysa)

### Seçenek A (tercih) — Yorum + dosya birlikte (tek işlem)
`POST .../yorumlar` ucunu **multipart/form-data** de kabul edecek şekilde genişletin: yorum ve ekleri **tek istekte** gider (temsilcinin doğal akışı: "yanıt + ekran görüntüsü").
```
POST https://destek.cmit.com.tr/api/talepler/{talepNo}/yorumlar
Content-Type: multipart/form-data
Authorization: Bearer <INTEGRATION_TOKEN>
X-User-Email: temsilci.eposta@...
```
Form alanları:
| Alan | Tip | Açıklama |
|---|---|---|
| `body` | metin | Yorum gövdesi (V2 ile aynı; boş olabilir, yalnız dosya da eklenebilir) |
| `isInternal` | `true`/`false` | Dahili not mu? **Ekler de yorumla aynı görünürlüğü alır** (iç not → müşteriye kapalı) |
| `files` | dosya (çoklu) | Bir veya birden çok dosya (aynı ada tekrar edilebilir: `files`, `files`) |

**Başarılı yanıt (`201`):** oluşturulan yorum + eklerin **kararlı id + indirme url'leri** (V2 `attachments[]` ile aynı şema):
```json
{
  "id": "c-1055",
  "externalRef": "26070300",
  "createdAt": "2026-07-28T16:20:00+03:00",
  "attachments": [
    {
      "id": "f-9001",
      "name": "ekran-goruntusu.png",
      "url": "https://destek.cmit.com.tr/api/talepler/26070300/dosya/f-9001",
      "sizeBytes": 84213,
      "contentType": "image/png",
      "uploadedAt": "2026-07-28T16:20:00+03:00"
    }
  ]
}
```

### Seçenek B (alternatif) — Bağımsız dosya yükleme ucu
Yorum genişletmek zorsa, talebe **doğrudan** dosya ekleyen ayrı bir uç:
```
POST https://destek.cmit.com.tr/api/talepler/{talepNo}/dosya
Content-Type: multipart/form-data
Authorization: Bearer <INTEGRATION_TOKEN>
X-User-Email: temsilci.eposta@...
```
Form alanı: `file` (tek dosya) — istenirse `isInternal` ile görünürlük.
**Başarılı yanıt (`201`):**
```json
{
  "id": "f-9001",
  "externalRef": "26070300",
  "name": "ekran-goruntusu.png",
  "url": "https://destek.cmit.com.tr/api/talepler/26070300/dosya/f-9001",
  "sizeBytes": 84213,
  "contentType": "image/png",
  "uploadedAt": "2026-07-28T16:20:00+03:00"
}
```

> Her iki seçenekte de dönen **`id`** ve **`url`**, V2 detay (`GET .../{no}` → `attachments[]`) ve indirme (`GET .../dosya/{id}`) ile **birebir aynı** olmalı ki biz dosyayı tekrar çekince kopyalamayalım (id ile dedup) ve indirebilelim.

---

## 4. Kısıtlar / kurallar

- **Boyut:** azami dosya boyutu limitiniz nedir? (Öneri: tek dosya ≤ 25 MB.) Aşımda `413` + `{ "error": "..." }`.
- **Tür:** izinli içerik türleri (öneri: görseller `png/jpg/gif/webp`, `pdf`, Office, `txt/log`, `zip`). Yasaklıda `415`.
- **Ad:** orijinal dosya adı korunsun (`Content-Disposition`/`name`).
- **Görünürlük:** `isInternal=true` ile yüklenen ek **müşteriye kapalı** (yalnız temsilci/yönetici), `false` ise müşteriye açık — yorum kuralıyla aynı.
- **Güvenlik:** yükleme aynı token + grup kısıtı (§2). Virüs/zararlı taraması sizde uygunsa yapılabilir.
- **İdempotentlik:** dosya yüklemesi doğası gereği idempotent değildir; tekrar denemede kopya oluşabilir. İsterseniz istemci `X-Idempotency-Key` başlığı gönderelim, aynı anahtarla ikinci istek yeni kopya oluşturmasın (opsiyonel). Aksi hâlde biz dönen `id` ile dedup ederiz.
- **Zaman damgaları:** ISO 8601 (+03:00). Hatalarda anlamlı HTTP kodu + `{ "error": "..." }`.

---

## 5. Bizde hazır olan

- V2 detay okuma (`attachments[]`) + `GET .../dosya/{id}` ile dosyaları **token'lı proxy** üzerinden indirip talep ekranında gösteriyoruz (çalışıyor).
- Talep yorum ekranımızda **Ctrl+V ile ekran görüntüsü** + dosya ekleme arayüzü hazır; yalnız **size gönderecek yükleme ucu** eksik. Uç açılınca istemciyi biz yazıp bağlarız.

---

## 6. Sizden ricamız (özet) ve açık sorular

**Açmanızı istediğimiz uç:** §3'teki **Seçenek A** (yorum + `files[]`) **veya** **Seçenek B** (`POST .../dosya`) — hangisi sizde pratikse.

**Netleştirmemiz gereken sorular:**
1. Bu uç Cortex Suite'te açılabilir mi? A mı B mi tercih edersiniz; yol/method/alan adları bizim önerimizle mi yoksa sizde farklı bir kalıpla mı olur?
2. Azami **dosya boyutu** ve **izinli içerik türleri** nedir?
3. Dönen `id`/`url`, V2 `attachments[]` + `GET .../dosya/{id}` ile aynı olacak şekilde verilebilir mi (dedup + indirme için)?
4. `isInternal` ile **ek görünürlüğü** (müşteriye kapalı/açık) yönetilebilir mi?
5. `X-Idempotency-Key` desteği mümkün mü, yoksa dedup'ı biz mi üstlenelim?

Dönüşünüze göre bizim taraftaki yükleme istemcisini hızlıca tamamlarız. Teşekkürler.
