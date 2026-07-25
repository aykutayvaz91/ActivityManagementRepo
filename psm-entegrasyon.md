# Sunucu Kurulum Talepleri Entegrasyon İstek Dokümanı
### psm.tdv.org (Sunucu Kurulum/Provizyon Sistemi) → Faaliyet Yönetim Sistemi

**Hazırlayan:** TDV/CMIT Altyapı Ekibi — Faaliyet Yönetim Sistemi (activitymanagement.tdv.org)
**Muhatap:** psm.tdv.org geliştirme/yönetim ekibi
**Konu:** Sunucu kurulum taleplerinin Faaliyet Yönetim Sistemi'ne otomatik aktarılması

---

## 1. Amaç ve bağlam

Altyapı ekibimiz tüm işlerini (görev, faaliyet ve **talep**) tek bir Faaliyet Yönetim Sistemi üzerinden yönetiyor ve **efor (harcanan süre)** takibini burada yapıyor.

**psm.tdv.org** üzerinden ekibimize düşen **sunucu kurulum talepleri**nin bu sisteme otomatik aktarılmasını istiyoruz. Talep bizde bir "iş kartı" olarak açılacak; sorumlu üzerine efor girecek, durum güncelleyecek.

psm talepleri **iki fazlıdır**: (1) **talep fazı** (ne istendi — talep eden, birim, gerekçe, istenen özellikler), (2) **kurulum fazı** (ne teslim edildi — hostname, IP, kurulan özellikler, servis/portlar). İkisini de aktarmak istiyoruz (**parolalar hariç**).

Bu doküman, entegrasyonun çalışması için **sizden ne beklediğimizi** ve **verinin hangi formatta gelmesi gerektiğini** tanımlar.

---

## 2. Nasıl çalışacak? (yöntem)

Tercih ettiğimiz yöntem **PULL**'dur: Bizim sistemimiz, sizin açacağınız bir **okuma API'sini** periyodik olarak (ör. 5–15 dakikada bir) çağırıp yeni/değişen talepleri çeker ve kendi tarafında oluşturur/günceller.

| Yöntem | Sizin yapmanız gereken | Tercih |
|---|---|---|
| **A. REST + JSON okuma API'si** | Talepleri listeleyen, token'lı bir HTTP ucu | **Önerilen** |
| **B. Webhook (push)** | Yeni/değişen talepte bizim uca POST atmak | Uygun |
| **C. Read-only DB view** | Bize salt-okunur bir görünüm + kullanıcı | Ağ uygunsa |

psm bir TDV iç uygulaması olduğundan, iç ekipçe **A** veya **B** eklemek pratik olacaktır.

---

## 3. Sizden istediğimiz API (Seçenek A)

### 3.1. Liste ucu
```
GET https://psm.tdv.org/api/kurulum-talepleri?updatedSince={ISO8601}&assignee={eposta}&page={n}
Authorization: {API anahtarı / token}
```

- **updatedSince**: Bu tarihten sonra oluşturulan/değişen kayıtlar (artımlı senkron). Kayıtta `updatedAt` alanı olmalı.
- **assignee / group**: Yalnız ekibimize/atananımıza düşen talepleri filtreleyin.
- **Sayfalama**: `page`/`nextPage`.
- **Auth**: Bize bir **API anahtarı/token** verin; HTTPS zorunlu.

### 3.2. Kayıt başına JSON (örnek — PAROLASIZ)
```json
{
  "items": [
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
      "status": "Tamamlandı",
      "createdAt": "2026-07-24T09:00:00+03:00",
      "updatedAt": "2026-07-24T09:53:00+03:00",
      "startedAt": "2026-07-24T09:00:00+03:00",
      "completedAt": "2026-07-24T09:53:00+03:00",

      "requested": {
        "os": "Ubuntu Server 24.04 LTS",
        "type": "Fiziksel",
        "cpu": 8, "ramGb": 16,
        "disk1Gb": 100, "disk2Gb": null,
        "env": "Prod", "location": null
      },
      "installed": {
        "hostname": "tdvcicd2tr1",
        "os": "Ubuntu Server 24.04 LTS",
        "type": "Fiziksel",
        "cpu": 8, "ramGb": 16,
        "disk1Gb": 100, "disk2Gb": null,
        "env": "Prod",
        "ip": "192.168.152.142",
        "iloIp": null, "dns": null, "mac": null,
        "location": null,
        "notes": null
      },
      "services": [
        { "name": "SSH", "port": 22, "protocol": "TCP", "open": true }
      ]
    }
  ],
  "nextPage": null
}
```

> **`requested` / `installed` / `services`** alt nesneleri, teslim edilen sunucunun teknik künyesidir; bizde talebin "Ek Bilgi/kurulum" verisi olarak saklanır. **Parola ve kullanıcı adı alanları KESİNLİKLE gelmemelidir** (§6).

---

## 4. Alan eşleştirmesi

### 4.1. Talep fazı
| Sizin alan (ekran) | JSON anahtarı | Bizdeki karşılığı | Açıklama |
|---|---|---|---|
| Talep id | `externalRef` | ExternalRef | **Zorunlu.** Kararlı benzersiz no (idempotent anahtar) |
| Talebin linki | `url` | ExternalUrl | Kartta "Portalda aç" |
| Başlık | `title` | Title | **Zorunlu** |
| Gerekçe + Ek | `description` | Description | Uzun gereksinim metni |
| Talep Eden | `requesterName` | RequesterName | |
| Birim | `requesterUnit` | ExtraInfo | |
| Talep Eden e-posta | `requesterEmail` | RequesterEmail | |
| Atayan | `assignedByEmail` | (ExtraInfo/audit) | |
| Atanan | `assigneeEmail` | AssignedEmployee | **E-posta ile** kişimize eşleriz |
| Öncelik | `priority` | Priority / PriorityScore | |
| Ağ | `network` | ExtraInfo | Statik/DHCP |
| Başlangıç | `startedAt` | ReceivedDate | |
| Tamamlanma | `completedAt` | ResolvedDate | |
| Durum | `status` | Status | Eşleme tablosu (§5) |

### 4.2. Kurulum fazı (psm'e özel)
| Grup | Alanlar |
|---|---|
| Sunucu | hostname, os, type, cpu, ramGb, disk1Gb, disk2Gb, env, location |
| Ağ | ip, iloIp, dns, mac |
| Servis/Portlar | name, port, protocol, open (liste) |
| İstenen vs Kurulan | `requested` ↔ `installed` (teslim, talebe uydu mu takibi) |
| **Kimlik (HARİÇ)** | ~~Admin Kullanıcı/Şifre, ILO Kullanıcı/Şifre~~ — gönderilmez |

---

## 5. Durum eşlemesi

Bizim durumlarımız: **Yeni · Atandı · Devam Ediyor · Beklemede · Çözüldü · Kapandı · İptal**

psm akışına göre (ekran: Bekliyor → Atandı → Kurulumda → Tamamlandı):

| psm durumu | Bizim durum |
|---|---|
| Bekliyor | Yeni |
| Atandı | Atandı |
| Kurulumda (bilgi kaydı) | Devam Ediyor |
| Tamamlandı | Çözüldü / Kapandı |
| İptal / Red (varsa) | İptal |

Tam durum listenizi iletin; eşlemeyi ona göre kesinleştiririz.

---

## 6. Güvenlik ve kapsam (KRİTİK)

- **PAROLLER VE KİMLİK BİLGİLERİ ASLA GÖNDERİLMEZ.** psm'deki **Admin Şifre, ILO Şifre, Admin Kullanıcı, ILO Kullanıcı** ve "Kayıtlı Şifreler" alanları payload'da **hiç yer almamalıdır**. Bizim sistemimizde sunucu parolası tutulmayacaktır.
- **Yalnız ilgili kayıtlar:** Tüm talepleri değil, **yalnız ekibimize/atananımıza** düşen kurulum taleplerini gönderin (assignee/grup filtresi).
- **Taşıma:** HTTPS + token zorunlu.
- **Ağ:** psm iç IP'li (192.168…). Bizim sunucumuzdan psm API'sine erişim açık olmalı.

---

## 7. Bizden ne bekliyoruz — kontrol listesi

1. **API'niz var mı?** (REST + JSON okuma ucu) — yoksa **webhook** ya da **read-only DB view** mümkün mü?
2. **BaseUrl** + **kimlik doğrulama** (API key / token) nedir? Anahtarı nasıl alırız?
3. `updatedSince` (artımlı) + **atanan/grup filtresi** + **sayfalama** destekleniyor mu?
4. **Tam durum listeniz** nedir? (özellikle iptal/red)
5. Payload'da **talep eden + atanan e-postası** var mı?
6. Kurulum alanlarından hangileri paylaşılabilir — **Admin/ILO parola ve kullanıcı adı kesin hariç**, onaylıyor musunuz?
7. Talepler bize hangi tarihten itibaren aktarılsın (geçmiş dahil mi)?
8. Bizim sunucumuzdan psm API'sine **ağ/firewall erişimi** açık mı?
9. psm'i **kim geliştiriyor** (iç ekip mi), değişiklik/uç eklenebilir mi?

---

## 8. Bizim tarafımızda hazır olan

- Talebi alan **webhook ucu** (`POST /api/integration/requests`, `X-Api-Key` doğrulamalı) hazır — Seçenek B'yi seçerseniz bu uca §3.2'deki JSON'u POST atmanız yeterli.
- İdempotent kayıt: aynı `externalRef` ile tekrar gelen kayıt kopyalanmaz, güncellenir.

**İletişim / sonraki adım:** Seçtiğiniz yöntemi ve §7 yanıtlarını iletmeniz yeterli; entegrasyon istemcisini biz yazıp bağlarız.
