---
name: abp-deploy
description: ActivityManagement'i (ABP/.NET 8, IIS in-process, port 8090) canlıya alma. dotnet build → yedek → app_offline → DLL kopya → app_offline sil → HTTP doğrula → migration doğrula. "deploy et / yayına al / canlıya çık" denince veya kod değişikliğini canlıda görmek gerektiğinde kullan.
---

# ABP Deploy — ActivityManagement

Kaynak: `C:\ActivityManagement` · Canlı: `C:\inetpub\ActivityManagement` (IIS in-process, app pool `ActivityManagement`, port 8090). Görünüm (.cshtml) `ActivityManagement.Web.Mvc.dll` içine gömülü derlenir; view değişse bile Web.Mvc.dll deploy edilir.

## Adımlar (bypass permissions modunda otomatik)

1. **Sürüm bump** (kural): kod/view değiştiyse `src/ActivityManagement.Core/ActivityManagementConsts.cs` → `AppVersion`. Şema `1.MAJOR.MINOR`, MINOR .1–.5; .5 sonrası MAJOR atla, MINOR .1'e döner (ör. `1.13.5 → 1.14.1`). `.0` kullanma.

2. **Build** (Web.Mvc tüm katmanları referanslar):
   ```bash
   cd /c/ActivityManagement
   dotnet build src/ActivityManagement.Web.Mvc/ActivityManagement.Web.Mvc.csproj -c Release --nologo -v q 2>&1 | grep -vE "query filter" | tail -6
   ```
   `0 Warning(s) 0 Error(s)` görmeden deploy etme.

3. **Deploy** (yalnız DEĞİŞEN katmanların DLL+PDB'si; migration varsa `EntityFrameworkCore` de):
   ```bash
   LIVE="/c/inetpub/ActivityManagement"; BINDIR="/c/ActivityManagement/src/ActivityManagement.Web.Mvc/bin/Release/net8.0"
   printf '<html><body>x</body></html>' > "$LIVE/app_offline.htm"; sleep 2   # in-process durur, DLL kilidi açılır
   for d in ActivityManagement.Core ActivityManagement.Application ActivityManagement.EntityFrameworkCore ActivityManagement.Web.Mvc; do
     cp -f "$BINDIR/$d.dll" "$LIVE/"; cp -f "$BINDIR/$d.pdb" "$LIVE/" 2>/dev/null; done
   rm -f "$LIVE/app_offline.htm"
   for i in $(seq 1 10); do code=$(curl -s -o /dev/null -w "%{http_code}" http://localhost:8090/ 2>/dev/null); [ "$code" = "302" -o "$code" = "200" ] && { echo "up: $code"; break; }; sleep 3; done
   ```
   - Migration varsa açılışta `Database.Migrate()` OTOMATİK uygular (Startup). Migration `dotnet ef migrations add` ile eklenirken **`--no-build` KULLANMA** (bayat assembly → yanlış diff). Deploy sonrası kolon/şemayı `INFORMATION_SCHEMA` ile doğrula.
   - IIS_IUSRS Modify izni gerekenler: `logs`, `wwwroot/uploads`.

4. **DOĞRULA (kanıt şart — iddia etme, göster):** `up: 302/200`. Değiştirdiğin davranışı canlıda ölç (curl ile endpoint, `scratchpad/dbq.ps1` ile DB, footer sürümü). Background/HostedService işini `run_in_background` poll ile bekle.

5. **Commit + push + history** → `git-commit-push` becerisine bak; `history/YYYY-MM-DD.md` güncelle (ne yapıldı, hangi dosyalar, deploy/DB durumu, doğrulama).

## Sık hatalar
- View değişti ama Web.Mvc.dll kopyalanmadı → değişiklik canlıda yok.
- Migration eklendi ama EntityFrameworkCore.dll deploy edilmedi → şema eski, "truncated"/kolon hatası.
- Sürüm bump unutuldu → footerda eski sürüm.
