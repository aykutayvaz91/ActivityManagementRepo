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
    // Otomatik arşiv: TAMAMLANDI görevler, tamamlandıkları AY geçtikten sonra (aylık rapor çekildikten sonra)
    // "Kapatıldı"ya çekilir. Yani CompletedDate'i, içinde bulunulan ayın 1'inden ÖNCE olan tüm tamamlanmış görevler kapatılır.
    // Kapatıldı görevler: geçmiş/tarih-kapsamlı raporlarda hâlâ "tamamlanmış iş" sayılır (CompletedDate korunur),
    // ancak AKTİF ilerleme %'lerinde hesaba KATILMAZ ve panoda görünmez (durum kolonu eşleşmez → arşiv).
    // Servis ~12 saatte bir çalışır (ay dönümünü kaçırmaz).
    public class TaskAutoCloseHostedService : BackgroundService
    {
        private static readonly TimeSpan LoopInterval = TimeSpan.FromHours(12);

        private readonly IServiceProvider _serviceProvider;

        public TaskAutoCloseHostedService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try { await Task.Delay(TimeSpan.FromMinutes(4), stoppingToken); } catch { } // açılışı bekle

            while (!stoppingToken.IsCancellationRequested)
            {
                try { await RunOnceAsync(); }
                catch (Exception ex) { ActivityManagement.Logging.ErrorLog.Write(ex, "TaskAutoCloseHostedService"); }

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

            using var uow = uowManager.Begin();
            var now = DateTime.Now;
            var firstOfThisMonth = new DateTime(now.Year, now.Month, 1);

            // Tamamlandı + tamamlanma tarihi bu aydan ÖNCE (tamamlandığı ay geçmiş) → Kapatıldı.
            var toClose = await taskRepo.GetAll()
                .Where(t => t.Status == Entities.TaskStatus.Tamamlandi
                            && t.CompletedDate.HasValue
                            && t.CompletedDate.Value < firstOfThisMonth)
                .ToListAsync();

            foreach (var t in toClose)
                t.Status = Entities.TaskStatus.Kapatildi; // CompletedDate KORUNUR (rapor/geçmiş için)

            await uow.CompleteAsync();
        }
    }
}
