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

namespace ActivityManagement.Web.BackgroundJobs
{
    // YILLIK ARŞİV — veri büyümesini sınırlar.
    // Kural: "içinde bulunulan yılın 1 Ocak'ından ÖNCEKİ" (yani geçmiş yıllara ait) kayıtlar arşivlenir.
    //   • SystemAuditLog → SystemAuditLogArchives tablosuna TAŞINIR (kopyalanır + sıcak tablodan silinir).
    //   • Notification    → eskiler doğrudan SİLİNİR (geçmiş yıla ait in-app bildirim değersizdir).
    // Servis ~24 saatte bir kontrol eder; iş idempotenttir (taşınmış kayıt tekrar bulunmaz), yıl dönümünde
    // önceki yıl otomatik arşivlenir → pratikte "yılda 1" çalışır. AG dostu: küçük partiler (BatchSize) hâlinde commit.
    public class ArchiveHostedService : BackgroundService
    {
        private static readonly TimeSpan LoopInterval = TimeSpan.FromHours(24);
        private const int BatchSize = 2000;

        private readonly IServiceProvider _serviceProvider;

        public ArchiveHostedService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try { await Task.Delay(TimeSpan.FromMinutes(6), stoppingToken); } catch { } // açılışı bekle

            while (!stoppingToken.IsCancellationRequested)
            {
                try { await RunOnceAsync(stoppingToken); }
                catch (Exception ex) { ActivityManagement.Logging.ErrorLog.Write(ex, "ArchiveHostedService"); }

                try { await Task.Delay(LoopInterval, stoppingToken); }
                catch { break; }
            }
        }

        private async Task RunOnceAsync(CancellationToken ct)
        {
            var cutoff = new DateTime(DateTime.Now.Year, 1, 1); // bu yılın 1 Ocak'ından öncesi = geçmiş yıllar

            // 1) Denetim kayıtları: parti parti TAŞI (kopyala + sil)
            int movedAudit = 0;
            while (!ct.IsCancellationRequested)
            {
                int n = await ArchiveAuditBatchAsync(cutoff);
                if (n == 0) break;
                movedAudit += n;
            }

            // 2) Eski bildirimler: parti parti SİL
            int purgedNotif = 0;
            while (!ct.IsCancellationRequested)
            {
                int n = await PurgeNotificationBatchAsync(cutoff);
                if (n == 0) break;
                purgedNotif += n;
            }

            if (movedAudit > 0 || purgedNotif > 0)
                ActivityManagement.Logging.ErrorLog.Write(
                    new Exception($"[BİLGİ] Yıllık arşiv: {movedAudit} denetim kaydı arşivlendi, {purgedNotif} eski bildirim silindi (kesim: {cutoff:dd.MM.yyyy})."),
                    "ArchiveHostedService");
        }

        private async Task<int> ArchiveAuditBatchAsync(DateTime cutoff)
        {
            using var scope = _serviceProvider.CreateScope();
            var sp = scope.ServiceProvider;
            var uowManager = sp.GetRequiredService<IUnitOfWorkManager>();
            var auditRepo = sp.GetRequiredService<IRepository<SystemAuditLog, long>>();
            var archiveRepo = sp.GetRequiredService<IRepository<SystemAuditLogArchive, long>>();

            using var uow = uowManager.Begin();
            var now = DateTime.Now;

            var batch = await auditRepo.GetAll()
                .Where(a => a.ExecutionTime < cutoff)
                .OrderBy(a => a.Id)
                .Take(BatchSize)
                .ToListAsync();

            if (batch.Count == 0) { await uow.CompleteAsync(); return 0; }

            foreach (var a in batch)
            {
                await archiveRepo.InsertAsync(new SystemAuditLogArchive
                {
                    OriginalId = a.Id,
                    TenantId = a.TenantId,
                    UserId = a.UserId,
                    UserName = a.UserName,
                    ExecutionTime = a.ExecutionTime,
                    ClientIpAddress = a.ClientIpAddress,
                    ActionType = a.ActionType,
                    EntityName = a.EntityName,
                    EntityId = a.EntityId,
                    OriginalValues = a.OriginalValues,
                    NewValues = a.NewValues,
                    ArchivedAt = now
                });
                await auditRepo.DeleteAsync(a);
            }

            await uow.CompleteAsync();
            return batch.Count;
        }

        private async Task<int> PurgeNotificationBatchAsync(DateTime cutoff)
        {
            using var scope = _serviceProvider.CreateScope();
            var sp = scope.ServiceProvider;
            var uowManager = sp.GetRequiredService<IUnitOfWorkManager>();
            var notifRepo = sp.GetRequiredService<IRepository<Notification, long>>();

            using var uow = uowManager.Begin();

            var batch = await notifRepo.GetAll()
                .Where(n => n.CreationTime < cutoff)
                .OrderBy(n => n.Id)
                .Take(BatchSize)
                .ToListAsync();

            if (batch.Count == 0) { await uow.CompleteAsync(); return 0; }

            foreach (var n in batch)
                await notifRepo.DeleteAsync(n);

            await uow.CompleteAsync();
            return batch.Count;
        }
    }
}
