# Destek Sistemi Entegrasyon İstek Dokümanı
### destek.cmit.com.tr (CMIT Bilişim – Destek Sistemi) → Faaliyet Yönetim Sistemi

**Hazırlayan:** TDV/CMIT Altyapı Ekibi — Faaliyet Yönetim Sistemi (activitymanagement.tdv.org)
**Muhatap:** destek.cmit.com.tr (Cortex Suite) yönetim/geliştirme ekibi
**Konu:** Destek taleplerinin Faaliyet Yönetim Sistemi'ne otomatik aktarılması

---

## 1. Amaç ve bağlam

Altyapı ekibimiz, üzerinde çalıştığı tüm işleri (görev, faaliyet ve **talep**) tek bir Faaliyet Yönetim Sistemi üzerinden yönetmek ve **efor (harcanan süre)** takibini burada yapmak istiyor.

**destek.cmit.com.tr** üzerinden ekibimize düşen **destek talepleri**nin, temsilcilerimizin manuel giriş yapmasına gerek kalmadan bu sisteme otomatik aktarılmasını istiyoruz. Talep bizde bir "iş kartı" olarak açılacak; temsilci üzerine efor girecek, durum güncelleyecek.

Bu doküman, entegrasyonun çalışması için **sizden ne beklediğimizi** ve **verinin hangi formatta gelmesi gerektiğini** tanımlar.

---

## 2. Nasıl çalışacak? (yöntem)

Tercih ettiğimiz yöntem **PULL**'dur: Bizim sistemimiz, sizin açacağınız bir **okuma API'sini** periyodik olarak (ör. 5–15 dakikada bir) çağırıp yeni/değişen talepleri çeker ve kendi tarafında oluşturur/günceller.

Uygulanabilir üç seçenek (birini seçelim):

| Yöntem | Sizin yapmanız gereken | Tercih |
|---|---|---|
| **A. REST + JSON okuma API'si** | Talepleri listeleyen, token'lı bir HTTP ucu | **Önerilen** |
| **B. Webhook (push)** | Yeni/değişen talepte bizim uca POST atmak | Uygun |
| **C. Read-only DB view** | Bize salt-okunur bir görünüm + kullanıcı | Ağ uygunsa |

Cortex Suite'in hâlihazırda bir API'si varsa **A seçeneği** en pratiğidir.

---

## 3. Sizden istediğimiz API (Seçenek A)

### 3.1. Liste ucu
```
GET https://destek.cmit.com.tr/api/talepler?updatedSince={ISO8601}&group={grup}&page={n}
Authorization: {API anahtarı / token}
```

- **updatedSince**: Bu tarihten sonra oluşturulan/değişen kayıtları döndürün (artımlı senkron için). Kayıtta bir `updatedAt` alanı da olmalı.
- **group**: Yalnız ilgili grubun taleplerini filtreleyin (aşağıya bakınız).
- **Sayfalama**: Çok kayıt varsa `page`/`nextPage` ile bölün.
- **Auth**: Bize bir **API anahtarı/token** verin; her istekte header'da göndereceğiz. HTTPS zorunlu.

### 3.2. Kayıt başına JSON (örnek)
```json
{
  "items": [
    {
      "externalRef": "26070052",
      "url": "https://destek.cmit.com.tr/talep/26070052",
      "title": "Veam backup",
      "description": "Son alınan canlı ortamdaki JGUAR datasının sıkıştırılmış hâliyle backup alabilir miyiz?",
      "requesterName": "Ahmet Çöpüroğlu",
      "requesterEmail": "ahmet.copuroglu@tdv.org",
      "assigneeEmail": "aykut.ayvaz@cmit.com.tr",
      "group": "Sistem ve Altyapı Operasyon",
      "category": "TEKNİK DESTEK",
      "problemType": "Sistem, Donanım, Altyapı",
      "priority": "Orta",
      "status": "Kapatıldı",
      "createdAt": "2026-07-03T10:37:00+03:00",
      "updatedAt": "2026-07-05T14:00:00+03:00",
      "dueDate": "2026-07-07T14:44:00+03:00",
      "resolvedAt": "2026-07-05T09:00:00+03:00",
      "closedAt": "2026-07-05T14:00:00+03:00"
    }
  ],
  "nextPage": null
}
```

---

## 4. Alan eşleştirmesi (sizin alan → bizim alan)

| Sizin alan (ekran) | JSON anahtarı | Bizdeki karşılığı | Açıklama |
|---|---|---|---|
| Talep No (`#26070052`) | `externalRef` | ExternalRef | **Zorunlu.** Kararlı benzersiz no; tekrar çekince kopyalanmaması için anahtar |
| Talebin linki | `url` | ExternalUrl | Kartta "Portalda aç" bağlantısı |
| Başlık | `title` | Title | **Zorunlu** |
| Açıklama | `description` | Description | |
| Oluşturan | `requesterName` | RequesterName | |
| Oluşturan e-posta | `requesterEmail` | RequesterEmail | |
| Atanan Temsilci | `assigneeEmail` | AssignedEmployee | **E-posta ile** kişimize eşleriz (isim eşleme kırılgan) |
| Atanan Grup | `group` | Team | Ekip/takım eşlemesi + filtre |
| Kategori / Sorun Tipi | `category`, `problemType` | Category / ExtraInfo | |
| Öncelik | `priority` | Priority / PriorityScore | Düşük/Orta/Yüksek/Kritik |
| Durum | `status` | Status | Eşleme tablosu (§5) |
| Oluşturulma | `createdAt` | ReceivedDate | |
| Güncelleme | `updatedAt` | (artımlı senkron) | `updatedSince` filtresini besler |
| SLA Son Çözüm | `dueDate` | DueDate | |
| Çözüm/Kapanış | `resolvedAt`, `closedAt` | ResolvedDate/ClosedDate | |

---

## 5. Durum eşlemesi

Bizim sistemimizin durumları: **Yeni · Atandı · Devam Ediyor · Beklemede · Çözüldü · Kapandı · İptal**

Sizin durum isimlerinizin tam listesini iletin; aşağıdaki gibi eşleyeceğiz (örnek):

| Sizin durum | Bizim durum |
|---|---|
| Açık / Yeni | Yeni |
| Atandı | Atandı |
| İşlemde | Devam Ediyor |
| Beklemede / Bilgi Bekliyor | Beklemede |
| Çözüldü | Çözüldü |
| Kapatıldı | Kapandı |
| İptal | İptal |

> **Not:** Bizde temsilcinin girdiği efor ve durum yereldir; portal yalnız "kapandı/iptal" bildirdiğinde talebi kapatırız, diğer yerel değişiklikleri ezmeyiz.

---

## 6. Güvenlik ve kapsam (önemli)

- **Yalnız ilgili kayıtlar:** Tüm sistemin taleplerini değil, **yalnız ekibimize/grubumuza** atanan talepleri gönderin (`group` filtresi, ör. "Sistem ve Altyapı Operasyon").
- **Kişisel veri:** Yalnız iş için gerekli alanları paylaşın (ad, e-posta, talep içeriği). Gereksiz kişisel veri gerekmez.
- **Taşıma:** HTTPS + token zorunlu.

---

## 7. Bizden ne bekliyoruz — kontrol listesi

Lütfen aşağıdakileri yanıtlayın / iletin:

1. **API'niz var mı?** (REST + JSON okuma ucu) — yoksa **webhook** atabilir misiniz, ya da **read-only DB view** verebilir misiniz?
2. **BaseUrl** ve **kimlik doğrulama** yöntemi (API key / bearer token) nedir? Anahtarı nasıl alırız?
3. `updatedSince` (artımlı) + **grup filtresi** + **sayfalama** destekleniyor mu?
4. **Tam durum listeniz** nedir? (eşleme tablosu için)
5. Payload'da **atanan ve talep eden e-postası** yer alıyor mu?
6. Talepler bize hangi tarihten itibaren aktarılsın (geçmiş dahil mi, sadece bundan sonrası mı)?
7. Bizim sunucumuzdan API'nize **ağ/firewall erişimi** açık mı?

---

## 8. Bizim tarafımızda hazır olan

- Talebi alan **webhook ucu** (`POST /api/integration/requests`, `X-Api-Key` doğrulamalı) hazır — Seçenek B'yi seçerseniz bu uca POST atmanız yeterli. İstek gövdesi §3.2'deki JSON'dur.
- İdempotent kayıt: aynı `externalRef` ile tekrar gelen kayıt kopyalanmaz, güncellenir.

**İletişim / sonraki adım:** Seçtiğiniz yöntemi ve §7 yanıtlarını iletmeniz yeterli; entegrasyon istemcisini biz yazıp bağlarız.
