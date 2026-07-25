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
    // Günlük SLA hatırlatması: son teslim tarihi YARIN olan, tamamlanmamış görevlerin atananlarına e-posta.
    // SMTP yapılandırılmamışsa AppEmailSender no-op olduğundan sessizce hiçbir şey göndermez (altyapı hazır).
    public class SlaReminderHostedService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;

        public SlaReminderHostedService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Uygulama açılışını bekle
            try { await Task.Delay(TimeSpan.FromMinutes(3), stoppingToken); } catch { }

            while (!stoppingToken.IsCancellationRequested)
            {
                try { await RunOnceAsync(); }
                catch (Exception ex) { ActivityManagement.Logging.ErrorLog.Write(ex, "SlaReminderHostedService"); }

                try { await Task.Delay(TimeSpan.FromHours(24), stoppingToken); }
                catch { break; }
            }
        }

        private async Task RunOnceAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var sp = scope.ServiceProvider;
            var uowManager = sp.GetRequiredService<IUnitOfWorkManager>();
            var taskRepo = sp.GetRequiredService<IRepository<TaskItem, long>>();
            var empRepo = sp.GetRequiredService<IRepository<Employee, long>>();
            var sender = sp.GetRequiredService<IAppEmailSender>();
            var notifier = sp.GetRequiredService<ActivityManagement.Notifications.INotificationManager>();

            using var uow = uowManager.Begin();
            var tomorrow = DateTime.Today.AddDays(1);
            var dayAfter = DateTime.Today.AddDays(2);

            var dueTasks = await taskRepo.GetAll()
                .Where(t => t.DueDate.HasValue && t.DueDate.Value >= tomorrow && t.DueDate.Value < dayAfter
                            && t.Status != Entities.TaskStatus.Tamamlandi
                            && t.Status != Entities.TaskStatus.Iptal
                            && t.AssignedEmployeeId != null)
                .ToListAsync();

            foreach (var t in dueTasks)
            {
                // In-app bildirim (personel kaydı olan atanana)
                await notifier.NotifyAsync(t.AssignedEmployeeId, Entities.NotificationType.SlaYaklasti,
                    "SLA yaklaşıyor", $"{t.Title} — son teslim {t.DueDate.Value:dd.MM.yyyy}",
                    $"/Tasks/Detail/{t.Id}", severity: "warning");

                var emp = await empRepo.GetAll().AsNoTracking()
                    .FirstOrDefaultAsync(e => e.Id == t.AssignedEmployeeId.Value);
                if (emp != null && !string.IsNullOrWhiteSpace(emp.Email))
                {
                    await sender.SendAsync(emp.Email,
                        $"SLA hatırlatması: {t.Title} yarın teslim",
                        $"<p>Merhaba {emp.FullName},</p><p><b>{System.Net.WebUtility.HtmlEncode(t.Title)}</b> görevinin son teslim tarihi <b>yarın ({t.DueDate.Value:dd.MM.yyyy})</b>.</p>");
                }
            }

            // Talepler: SLA'sı yarın olan, kapanmamış, atanan talepler → in-app bildirim
            var reqRepo = sp.GetRequiredService<IRepository<ServiceRequest, long>>();
            var dueReqs = await reqRepo.GetAll()
                .Where(r => r.DueDate.HasValue && r.DueDate.Value >= tomorrow && r.DueDate.Value < dayAfter
                            && r.Status != RequestStatus.Kapandi && r.Status != RequestStatus.Iptal
                            && r.AssignedEmployeeId != null)
                .ToListAsync();
            foreach (var r in dueReqs)
            {
                await notifier.NotifyAsync(r.AssignedEmployeeId, Entities.NotificationType.SlaYaklasti,
                    "Talep SLA yaklaşıyor", $"{r.Title} — hedef {r.DueDate.Value:dd.MM.yyyy}",
                    $"/Requests/Detail/{r.Id}", severity: "warning");
            }

            await uow.CompleteAsync();
        }
    }
}
