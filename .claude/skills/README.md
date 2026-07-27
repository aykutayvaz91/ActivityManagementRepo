# .claude/skills — ActivityManagement'e özel beceriler

obra/superpowers metodolojisinden (brainstorm → plan → uygula → kanıtla → review) esinlenip **bu repoya uyarlanmış**, dışarıdan kod çekmeden yazılmış Claude Code becerileri. Repo ile birlikte versiyonlanır; bu projede çalışan herkeste (Claude Code) devreye girer.

Beceriler `Skill` aracıyla veya ilgili iş geldiğinde otomatik önerilir:

| Beceri | Ne zaman |
|---|---|
| **brainstorm-plan-verify** | Önemsiz olmayan her iş için ana yöntem (anla→planla→uygula→**kanıtla**). |
| **abp-deploy** | Canlıya alma (build → app_offline → DLL kopya → doğrula → migration). |
| **git-commit-push** | GitHub'a commit + push (token maskeli) + history. |
| **code-review** | Canlıya almadan önce ABP/yetki/güvenlik kontrol listesi. |
| **db-query** | dbq.ps1 ile güvenli (read-only öncelikli) DB doğrulama. |

İlke: **kanıt > iddia**, karmaşıklığı azalt, küçük doğrulanabilir adımlar. Ayrıntılı proje kuralları: kök `CLAUDE.md` + `history/`.

Not (tam superpowers framework'ü isteyen için): Claude Code CLI'de `/plugin install superpowers@claude-plugins-official`. Bu klasördeki beceriler onunla çakışmaz; bunlar repoya-özgü işleri (deploy/dbq vb.) kapsar.
