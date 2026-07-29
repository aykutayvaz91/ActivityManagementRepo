# Yapılacaklar — Backlog (kaydedildi 2026-07-29, henüz UYGULANMADI)

> Kullanıcı "bunları yapılacaklar listesine kaydet, yapma şimdilik" dedi. Aşağıdaki 4 yeni madde + denetimden çıkan Orta/Düşük/eksik-özellik maddeleri bekliyor. "Devam et"te bu dosya + `history/2026-07-29.md` okunmalı.

## Yeni istekler (2026-07-29, öncelikli)

### Y1 — Talebe resim + iç not eklerken "portal yorumu reddetti" hatası
- **Belirti:** Bir talebe **resim koyup iç not** eklendiğinde "Portal yorumu reddetti" hatası alınıyor.
- **Kök neden:** Destek `WriteBackEnabled` açık; iç not da portala POST ediliyor. Resim eklenince istek **multipart** oluyor; **destek'in `/yorumlar` ucu multipart'ı reddediyor** (bilinen destek bug'ı — `destek-entegrasyon-v3-hata-bildirimi.md`). Yani resimli (multipart) iç not, destek düzeltene kadar başarısız + yerelde de kaydedilmiyor.
- **Yapılacak (bizim taraf, destek beklemeden):** İç not'ta (isInternal=true) resim/dosya varsa ve destek multipart reddederse iç notu **yerelde sakla** (portala gönderim opsiyonel/başarısızlığa dayanıklı olsun); ya da iç notların dosyalarını multipart yerine yerelde tut. Dış not için destek düzeltmesi şart. Karar netleştirilip uygulanacak.

### Y2 — Faaliyetler (Activities) görünürlük + arama
- Uzman: yalnız **kendi faaliyetlerini + kendisine atanan faaliyet başlıklarını** görsün.
- **Admin / TakımLideri: hepsini** görsün.
- **Arama** eklensin (başlık vb. ile hızlı bulma) — server-side.
- (Not: talep tarafındaki `ApplyVisibilityScope`/Query desenine benzer kurulacak; `ActivitySubjectAppService`.)

### Y3 — "Çözüldü" ≠ "Kapatılan Talep"
- Şu an arşiv (Kapatılan Talepler) = (Kapandı **veya Çözüldü**) && eforu var (B7).
- **İstenen:** Çözüldü talep **kapatılan sayılmasın**; yalnız **Kapatıldı/Kapandı** yapılınca "Kapatılan Talepler"e düşsün. Çözüldü aktifte/açık kalsın.
- Dosyalar: `ServiceRequestAppService.GetIndexAsync` (Archived tanımı), ilgili sayaç/sekme mantığı.

### Y4 — Denetim Orta + Düşük maddeleri (aşağıdaki liste) uygulanacak.

---

## Denetimden (2026-07-29) — ORTA
- **EmployeeAppService** `Delete`/`Update`: TakımLideri için takım-kapsamı yok → başka takımın/Admin'in personelini silebilir/düzenleyebilir. Kapsam kontrolü ekle (Admin/Manager hariç).
- **Detay senkronu watermark sırası:** watermark detay döngüsünden ÖNCE ilerliyor → transient hatada yorum/dosya kalıcı kaybı. Watermark'ı detay sonrası ilerlet veya son-N-gün yeniden dene. (`RequestSyncHostedService`)
- **PortalStatusText normalize:** ingest'te kod/etiket tutarsız saklanıyor → durum dropdown flip-flop. Katalog etiketine normalize et.
- **Yorum write-back çift kayıt:** POST cevabı id vermezse `ExternalCommentId=null` → sonraki senkron aynı yorumu tekrar ekler. cid null ise yerel ekleme veya (body+tarih) dedup.
- **Yetki yardımcıları tekrarı (en büyük borç):** `CurrentContext`/`IsManager`/`SeesAllTeams`/`EffectiveRole`/`CurrentEmployeeTeamId` vb. 12 dosyada ~50x kopya → `ActivityManagementAppServiceBase`'e (veya `ICurrentUserContext`) taşı.
- **`AsNoTracking` tutarsız:** Reports/TaskItem/ActivityLog/Project/Employee/Team okuma uçlarına ekle.
- **GetTeamReport N+1** (`ReportAppService`) → tek sorgu + GroupBy. **ProjectAppService.GetAllAsync** koleksiyon Include + paging kartezyen → projeksiyon/AsSplitQuery.
- **Application'a portal HTTP sızıntısı** (~350 satır HttpClient + auth-header 4-5x tekrar) → `IPortalClient` altyapı servisine çıkar.
- **Controller null-safety/try-catch:** `TasksController.Edit` (GET null), `Delete` (try/catch yok) → Requests desenine hizala. `TaskItemAppService.AddCommentAsync` senkron `.Get` → async.

## Denetimden (2026-07-29) — DÜŞÜK
- Güvenlik başlıkları: CSP + `X-Frame-Options` (nosniff eklendi). Webhook/admin login sabit-zamanlı karşılaştırma + rate-limit/lockout. Admin fallback parola `Admin123!` kaldır. Sayfa ACL fail-open (`catch{}` → CanAccessPage true). AccessControl okuma metotları yetkisiz (rol yapısı sızar).
- Ölü kod: `RoutineTask` (şema var, mantık yok — tamamla ya da kaldır), `ParentTaskId`, kullanılmayan `WireItem` alanları, çift `HoursSpent` kontrolü, kullanılmayan `IsAdmin`.
- `ActivityTypeLabels` 3 yerde tekrar + Reports'ta sınır kontrolü yok.

## Eksik özellik / geliştirme
- **PDF raporlama** (Faz 4; yalnız Excel var). **Gantt bağımlılıkları/kritik yol** (ParentTaskId'yi canlandır). **Global arama** (üst bar). **Toplu (bulk) işlemler**. **Bildirim tercihleri**. Kişi kartı efor trendi.
- **CLAUDE.md "Bekleyen Fazlar"ı güncelle** (Faz 3 tamam, Faz 4 büyük oranda bitmiş).

## Entegrasyon (bekleyen)
- **Destek V3 multipart bug:** bizim istemci hazır; destek ucu multipart'ı reddediyor (Y1 ile ilişkili). `destek-entegrasyon-v3-hata-bildirimi.md` iletilecek.
- **PSM ayrı tasarım** (farklı format). Write-back durum POST kod/etiket teyidi (ilk canlı yazımda).
