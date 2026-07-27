---
name: db-query
description: ActivityManagement veritabanını (AlwaysOn AG listener tdvsqlagl1tr1, DB ActivityManagement) güvenle sorgulama/doğrulama. scratchpad'deki dbq.ps1 ile. "DB'de kontrol et / kaç kayıt / şu tabloya bak / veriyi doğrula" gibi durumlarda kullan.
---

# DB Sorgu — dbq.ps1 (read-only yardımcı)

Bağlantı dizesi appsettings.Production.json'da DPAPI şifreli; dbq.ps1 içeride çözer, **ASLA ekrana/dosyaya yazmaz**. Bu kuralı bozma.

## Kullanım
`dbq.ps1` scratchpad'de. SQL'i **tek argüman** olarak ver (`-Query` gibi bir bayrak YOK — remaining-args ile alır):
```bash
SP="<scratchpad>"   # dbq.ps1'in bulunduğu dizin
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$SP/dbq.ps1" "SELECT COUNT(*) c FROM ServiceRequests WHERE Source=1"
```
- SELECT/WITH → satırları yazdırır. Türkçe karakter konsolda bozuk görünebilir (kod sayfası) — sorun değil, veri doğru.
- Tek kolon + tablo formatında parse için token kullan: `SELECT 'X='+CAST(col AS varchar) ...` → `grep -oE 'X=...'`.

## Güvenlik / kapsam
- Öncelik **read-only**. dbq.ps1 teknik olarak non-SELECT de çalıştırır; **veri düzeltmesi (UPDATE/DELETE)** yalnızca: (a) kullanıcı isteğiyle veya (b) kendi oluşturduğun test verisini temizlerken. Öncesinde etkilenecek satırı `SELECT` ile gör, `WHERE`'i daraltıp doğrula.
- Bağlantı dizesi/şifre asla yazdırılmaz. Şifreli sütun (ApiKey/şifre) içeriğini çözüp gösterme.

## Sık doğrulamalar
- Şema: `SELECT DATA_TYPE, CHARACTER_MAXIMUM_LENGTH FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='X' AND COLUMN_NAME='Y'` (max = -1 → nvarchar(max)).
- Migration uygulandı mı: ilgili kolon/tablo `INFORMATION_SCHEMA`'da var mı.
- Entegrasyon durumu: `IntegrationSources` (Enabled/BaseUrl/AuthHeader/UserEmail/LastResult), `ServiceRequests` (Source/Status/AssignedEmployeeId).
