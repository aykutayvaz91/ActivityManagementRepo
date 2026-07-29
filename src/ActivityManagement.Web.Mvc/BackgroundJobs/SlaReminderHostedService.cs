using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abp.Domain.Repositories;
using Abp.Domain.Uow;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ActivityManagement.Entities;
using ActivityManagement.Notifications;

namespace ActivityManagement.Web.BackgroundJobs
{
    // SLA hatırlatması: son teslim/hedef tarihi ÖNÜMÜZDEKİ 24 SAAT içinde olan, kapanmamış görev/taleplerin
    // atananlarına in-app bildirim (+ SMTP varsa e-posta). Servis 2 SAATTE BİR çalışır; her görev/talep için
    // EN FAZLA 3 hatırlatma gönderilir, aralarında en az ~2 saat olur (Notification tablosundan sayım/son-zaman).
    public class SlaReminderHostedService : BackgroundService
    {
        private const int MaxReminders = 3;                                  // görev/talep başına en fazla
        private static readonly TimeSpan MinGap = TimeSpan.FromHours(2);     // hatırlatmalar arası asgari süre
        private static readonly TimeSpan LoopInterval = TimeSpan.FromHours(2);
        private static readonly TimeSpan Lookahead = TimeSpan.FromHours(24); // "yaklaşıyor" penceresi

        private readonly IServiceProvider _serviceProvider;

        public SlaReminderHostedService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try { await Task.Delay(TimeSpan.FromMinutes(3), stoppingToken); } catch { } // açılışı bekle

            while (!stoppingToken.IsCancellationRequested)
            {
                try { await RunOnceAsync(); }
                catch (Exception ex) { ActivityManagement.Logging.ErrorLog.Write(ex, "SlaReminderHostedService"); }

                try { await Task.Delay(LoopInterval, stoppingToken); }
                catch { break; }
            }
        }

        private async Task RunOnceAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var sp = scope.ServiceProvider;
            var uowManager = sp.GetRequiredService<IUnitOfWorkManager>();
            var taskRepo = sp.GetRequiredService<IRepository<TaskItem, long>>();
            var reqRepo = sp.GetRequiredService<IRepository<ServiceRequest, long>>();
            var empRepo = sp.GetRequiredService<IRepository<Employee, long>>();
            var notifRepo = sp.GetRequiredService<IRepository<Notification, long>>();
            var sender = sp.GetRequiredService<IAppEmailSender>();
            var notifier = sp.GetRequiredService<INotificationManager>();

            using var uow = uowManager.Begin();
            var now = DateTime.Now;
            var limit = now.Add(Lookahead);

            // --- Görevler: SLA'sı önümüzdeki 24 saatte, geçmemiş, kapanmamış, atanmış ---
            var dueTasks = await taskRepo.GetAll()
                .Where(t => t.DueDate.HasValue && t.DueDate.Value > now && t.DueDate.Value <= limit
                            && t.Status != Entities.TaskStatus.Tamamlandi
                            && t.Status != Entities.TaskStatus.Kapatildi
                            && t.Status != Entities.TaskStatus.Iptal
                            && t.AssignedEmployeeId != null)
                .ToListAsync();

            foreach (var t in dueTasks)
            {
                var link = $"/Tasks/Detail/{t.Id}";
                if (!await ShouldRemindAsync(notifRepo, t.AssignedEmployeeId.Value, link, now, MaxReminders, MinGap)) continue;

                await notifier.NotifyAsync(t.AssignedEmployeeId, Entities.NotificationType.SlaYaklasti,
                    "SLA yaklaşıyor", $"{t.Title} — son teslim {t.DueDate.Value:dd.MM.yyyy HH:mm}",
                    link, severity: "warning");

                var emp = await empRepo.GetAll().AsNoTracking().FirstOrDefaultAsync(e => e.Id == t.AssignedEmployeeId.Value);
                if (emp != null && !string.IsNullOrWhiteSpace(emp.Email))
                {
                    // E-posta hatası tek işi etkiler, tüm turu KESMEZ (in-app bildirim zaten gitti).
                    try
                    {
                        await sender.SendAsync(emp.Email,
                            $"SLA hatırlatması: {t.Title}",
                            $"<p>Merhaba {emp.FullName},</p><p><b>{System.Net.WebUtility.HtmlEncode(t.Title)}</b> görevinin son teslim tarihi yaklaşıyor: <b>{t.DueDate.Value:dd.MM.yyyy HH:mm}</b>.</p>");
                    }
                    catch (Exception mex) { ActivityManagement.Logging.ErrorLog.Write(mex, "SlaReminder/Email"); }
                }
            }

            // --- Talepler: SLA'sı önümüzdeki 24 saatte, geçmemiş, kapanmamış, atanmış ---
            var dueReqs = await reqRepo.GetAll()
                .Where(r => r.DueDate.HasValue && r.DueDate.Value > now && r.DueDate.Value <= limit
                            && r.Status != RequestStatus.Kapandi && r.Status != RequestStatus.Iptal
                            && r.AssignedEmployeeId != null)
                .ToListAsync();

            foreach (var r in dueReqs)
            {
                var link = $"/Requests/Detail/{r.Id}";
                if (!await ShouldRemindAsync(notifRepo, r.AssignedEmployeeId.Value, link, now, MaxReminders, MinGap)) continue;

                await notifier.NotifyAsync(r.AssignedEmployeeId, Entities.NotificationType.SlaYaklasti,
                    "Talep SLA yaklaşıyor", $"{r.Title} — hedef {r.DueDate.Value:dd.MM.yyyy HH:mm}",
                    link, severity: "warning");
            }

            // --- SLA İHLALİ (H5): son teslim GEÇMİŞ, hâlâ açık işler → TAKIM LİDERİNE eskalasyon ---
            // (son 30 gün içinde ihlal olanlar; lidere en fazla 3 kez, aralarında ≥ 24 saat)
            var breachFloor = now.AddDays(-30);
            var teamLeaders = await empRepo.GetAll().AsNoTracking()
                .Where(e => e.TeamId != null)
                .Join(sp.GetRequiredService<IRepository<Team, long>>().GetAll().AsNoTracking(),
                      e => e.TeamId, t => t.Id, (e, t) => new { EmpTeamId = t.Id, t.LeaderId })
                .Where(x => x.LeaderId != null)
                .Distinct()
                .ToDictionaryAsync(x => x.EmpTeamId, x => x.LeaderId.Value);

            var breachedTasks = await taskRepo.GetAll()
                .Where(t => t.DueDate.HasValue && t.DueDate.Value < now && t.DueDate.Value >= breachFloor
                            && t.Status != Entities.TaskStatus.Tamamlandi
                            && t.Status != Entities.TaskStatus.Kapatildi
                            && t.Status != Entities.TaskStatus.Iptal
                            && t.TeamId != null)
                .ToListAsync();
            foreach (var t in breachedTasks)
            {
                if (!t.TeamId.HasValue || !teamLeaders.TryGetValue(t.TeamId.Value, out var leaderId)) continue;
                var link = $"/Tasks/Detail/{t.Id}";
                if (!await ShouldRemindAsync(notifRepo, leaderId, link, now, EscalationMax, EscalationGap)) continue;
                await notifier.NotifyAsync(leaderId, Entities.NotificationType.SlaYaklasti,
                    "SLA İHLALİ (eskalasyon)", $"{t.Title} — son teslim {t.DueDate.Value:dd.MM.yyyy HH:mm} GEÇTİ, iş hâlâ açık.",
                    link, severity: "danger");
            }

            var breachedReqs = await reqRepo.GetAll()
                .Where(r => r.DueDate.HasValue && r.DueDate.Value < now && r.DueDate.Value >= breachFloor
                            && r.Status != RequestStatus.Kapandi && r.Status != RequestStatus.Iptal
                            && r.TeamId != null)
                .ToListAsync();
            foreach (var r in breachedReqs)
            {
                if (!r.TeamId.HasValue || !teamLeaders.TryGetValue(r.TeamId.Value, out var leaderId)) continue;
                var link = $"/Requests/Detail/{r.Id}";
                if (!await ShouldRemindAsync(notifRepo, leaderId, link, now, EscalationMax, EscalationGap)) continue;
                await notifier.NotifyAsync(leaderId, Entities.NotificationType.SlaYaklasti,
                    "Talep SLA İHLALİ (eskalasyon)", $"{r.Title} — hedef {r.DueDate.Value:dd.MM.yyyy HH:mm} GEÇTİ, talep hâlâ açık.",
                    link, severity: "danger");
            }

            await uow.CompleteAsync();
        }

        private const int EscalationMax = 3;
        private static readonly TimeSpan EscalationGap = TimeSpan.FromHours(24);

        // Bu alıcı+link için bildirim gönderilmeli mi: toplam < max VE son bildirimden bu yana ≥ gap.
        private static async Task<bool> ShouldRemindAsync(IRepository<Notification, long> notifRepo, long recipientId, string link, DateTime now, int max, TimeSpan gap)
        {
            var q = notifRepo.GetAll().Where(n => n.RecipientEmployeeId == recipientId
                && n.Type == NotificationType.SlaYaklasti && n.Link == link);
            var count = await q.CountAsync();
            if (count >= max) return false;
            if (count > 0)
            {
                var lastAt = await q.OrderByDescending(n => n.Id).Select(n => n.CreationTime).FirstOrDefaultAsync();
                if (now - lastAt < gap) return false;
            }
            return true;
        }
    }
}
