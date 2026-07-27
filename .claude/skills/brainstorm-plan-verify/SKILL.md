---
name: brainstorm-plan-verify
description: ActivityManagement'te önemsiz olmayan her iş için çalışma yöntemi — anla/beyin fırtınası → (belirsizse) onay → doğru ABP katmanında uygula → canlıda KANITLA → commit/push/history/sürüm. Yeni özellik, bug fix, refactor veya "şunu yap/ekle/düzelt" gibi çok adımlı işlerde uygula.
---

# Yöntem: Anla → Planla → Uygula → Kanıtla (superpowers uyarlaması)

İlke: **kanıt > iddia**, **karmaşıklığı azalt**, **küçük doğrulanabilir adımlar**.

## 1) Anla / Beyin fırtınası
- İsteği net anla. Kapsamı belirsizse VEYA karar kullanıcıya aitse (yön, davranış tercihi) **önce sor** (AskUserQuestion) — yanlış yöne kod yazma.
- İlgili kodu oku: hangi katman, mevcut desen ne. Benzer işleri `history/` ve CLAUDE.md'den kontrol et.

## 2) Doğru katmanda uygula (Clean Architecture / ABP)
- Domain(Core) → Application(AppService/DTO/AutoMapper) → EntityFrameworkCore → Web(lean controller/MVC). Katman ihlali yok.
- Kurallar: `async/await`; read-only sorguda `.AsNoTracking()`; server-side paging/filtre.
- **Yetki:** `[AbpAuthorize]` YOK; `IHttpContextAccessor` claim (Role=AppRole, EmployeeId) ile manuel. Roller: Admin, Manager, TakımLideri, Uzman.
- **Görünürlük vs işlem:** görünürlük takım/kişi bazlı; işlem "kendine ait"e sınırlı; Admin/Manager geniş. login-as'te rol claim Admin kalır → kapsam kararlarında temsil edilen kişinin **gerçek AppRole**'üne bak.
- **Hata yönetimi:** sayfa patlamamalı — beklenen `UserFriendlyException`+TempData/redirect; beklenmeyen `ErrorLog.Write`; null kontrolü; AJAX'ta uygun HTTP kod+mesaj.
- **Tarih:** `dd.MM.yyyy` (global tr-TR). Sırlar DPAPI şifreli, ekrana yazma.

## 3) KANITLA (bitti demeden önce)
- Build `0/0`. Deploy → [abp-deploy].
- Davranışı **canlıda ölç**: curl endpoint (HTTP kodu/gövde), `scratchpad/dbq.ps1` (DB durumu), footer sürümü. Rol/kapsam işlerinde `Admin/ActAs` ile farklı kimliklerle test et (login-as rol claim'i Admin tutar — sınırını hatırla).
- Test verisi oluşturduysan **temizle**. Arka plan/HostedService işini `run_in_background` poll ile bekle, sonucu göster.

## 4) Kapat
- Commit + push (token maskeli) → [git-commit-push]. `history/YYYY-MM-DD.md` güncelle. Gerekirse [code-review] ile gözden geçir.

## Yapma
- Doğrulamadan "tamam" deme. Katman atlama. Migration'ı `--no-build` ile ekleme. Sır yazdırma. Hiyerarşik üste görev atatma.
