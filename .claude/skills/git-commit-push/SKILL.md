---
name: git-commit-push
description: ActivityManagement değişikliklerini GitHub'a (aykutayvaz91/ActivityManagementRepo) commit + push etme; token maskeli, doğru commit mesajı ve history güncellemesiyle. "githuba at / commit et / push et / kaydet" denince kullan.
---

# Git Commit + Push — ActivityManagement

Repo GitHub'da: `aykutayvaz91/ActivityManagementRepo` (main). Kullanıcı her anlamlı işi canlıya alıp GitHub'a atıyor.

## Akış
1. Değişiklikleri gözden geçir: `git status --porcelain`, gerekiyorsa [code-review].
2. Ekle + commit (mesaj sonunda Co-Authored-By satırı ZORUNLU):
   ```bash
   cd /c/ActivityManagement
   git add -A
   git commit -q -m "$(cat <<'EOF'
   <özet: ne + neden + sürüm (vX.Y.Z)>

   - <değişiklik maddeleri; dosya/davranış>
   - Doğrulama: <canlı kanıt kısa>

   Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
   EOF
   )"
   ```
3. **Push — token MASKELE** (log/çıktıya token sızmasın):
   ```bash
   git push origin HEAD 2>&1 | sed -E 's/x-access-token:[^@]*@/***@/g' | tail -2
   ```
4. **history** güncelle: `history/YYYY-MM-DD.md` (en güncel dosya) — ne yapıldı, hangi dosyalar, deploy/DB durumu, doğrulama, bekleyenler. Ayrı bir "history: ..." commit'i olarak da push edilebilir.

## Kurallar
- Default branch'te doğrudan çalışılıyor (bu repo öyle akıyor); ama commit/push YALNIZCA kullanıcı istediğinde.
- Hook atlama (`--no-verify`), imza bypass yok.
- `CRLF will be replaced` uyarıları normal (Windows), yok say.
- Sürüm bump'ı commit'e dahil et (footer/rapor için). Bkz [abp-deploy] sürüm şeması.
