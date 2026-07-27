---
name: code-review
description: ActivityManagement değişikliklerini canlıya almadan önce ABP/proje kurallarına göre gözden geçirme kontrol listesi. "gözden geçir / review / kontrol et / PR'a bakmadan önce" veya riskli/çok dosyalı değişikliklerden sonra kullan.
---

# Code Review — ActivityManagement

Değişen dosyaları (`git diff`) şu eksenlerde denetle. Bulguları en kritikten başlayarak, dosya:satır ile ver.

## Doğruluk
- Null kontrolü (GetAsync/tekil dönüş) — view'a null model gitmiyor.
- Enum/switch tam mı (yeni durum eklendiyse tüm map'ler: label/renk/ProgressForStatus/rapor filtreleri).
- Sınır/tarih/decimal (InvariantDecimalModelBinder) doğru.

## ABP / Katman
- Katman ihlali yok (Core→App→EF→Web). İş mantığı AppService'te, controller lean.
- `async/await`; read-only `.AsNoTracking()`; paging/filtre server-side.

## Yetki & Görünürlük (ZORUNLU)
- `[AbpAuthorize]` yok; claim ile manuel kontrol.
- Görünürlük (takım/kişi) vs işlem ("kendine ait"/CanEdit/CanManage/CanLogEffort) ayrımı korunmuş.
- **login-as tuzağı:** rol claim Admin kalır → kapsam kararı temsil edilen kişinin **DB AppRole**'üne bakmalı (yalnız claim'e değil).
- Manager takımsız + tüm takımları görür; hiyerarşik üste görev atanamaz; Sistem Yöneticisi dropdown/sayımda görünmez.

## Güvenlik
- Sır (apiKey/şifre/connection) DPAPI şifreli; ekrana/dosyaya/loga düz metin YAZILMAZ; git push token maskeli.
- Dış içerik (portal HTML) render'da sanitize (SafeHtml). Dynamic API'de non-admin guard.

## Hata yönetimi
- try/catch: beklenen→UserFriendlyException+TempData/redirect; beklenmeyen→ErrorLog.Write; AJAX→HTTP kod+JSON.
- Toplu işlemler (senkron upsert vb.) tek kötü kayıtta tüm partiyi düşürmüyor; alanlar kolon sınırına kırpılıyor.

## Performans/veri
- N+1 yok (Include). Migration `--no-build`'siz eklendi. Kolon boyutları içeriğe yetiyor (uzun HTML → nvarchar(max)).

## Kanıt
- Build 0/0; canlıda ölçülmüş doğrulama var; test verisi temizlenmiş.
