using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abp.Application.Services;
using Abp.Domain.Repositories;
using Abp.Linq.Extensions;
using Microsoft.EntityFrameworkCore;
using ActivityManagement.Entities;
using ActivityManagement.Reports.Dto;

namespace ActivityManagement.Reports
{
    // Yetki cookie claim tabanlı (global [Authorize] filtresi giriş zorunluluğunu sağlar).
    public class ReportAppService : ActivityManagementAppServiceBase, IReportAppService
    {
        private static readonly string[] ActivityTypeLabels = new[]
        {
            "Bakım","Geliştirme","Kurulum","Destek","Test","Dokümantasyon","Eğitim","Analiz","Proje","Diğer"
        };

        private readonly IRepository<Employee, long> _employeeRepository;
        private readonly IRepository<ActivityLog, long> _activityRepository;
        private readonly IRepository<TaskItem, long> _taskRepository;

        public ReportAppService(
            IRepository<Employee, long> employeeRepository,
            IRepository<ActivityLog, long> activityRepository,
            IRepository<TaskItem, long> taskRepository)
        {
            _employeeRepository = employeeRepository;
            _activityRepository = activityRepository;
            _taskRepository = taskRepository;
        }

        public async Task<PersonalReportDto> GetPersonalReportAsync(GetReportInput input)
        {
            if (!input.EmployeeId.HasValue || input.EmployeeId.Value <= 0)
                throw new Abp.UI.UserFriendlyException("Rapor için personel seçilmedi.");
            var employee = await _employeeRepository.GetAll().AsNoTracking().FirstOrDefaultAsync(e => e.Id == input.EmployeeId.Value);
            if (employee == null)
                throw new Abp.UI.UserFriendlyException("Seçilen personel bulunamadı.");

            var activities = await _activityRepository.GetAll().AsNoTracking()
                .Include(a => a.Project)
                .Include(a => a.TaskItem)
                .Include(a => a.ActivitySubject)
                .Include(a => a.ServiceRequest)
                .Where(a => a.EmployeeId == input.EmployeeId.Value &&
                            a.ActivityDate >= input.StartDate &&
                            a.ActivityDate <= input.EndDate)
                .ToListAsync();

            var tasks = await _taskRepository.GetAll().AsNoTracking()
                .Include(t => t.Project)
                .Where(t => t.AssignedEmployeeId == input.EmployeeId.Value)
                .ToListAsync();

            var report = new PersonalReportDto
            {
                EmployeeId = employee.Id,
                EmployeeName = employee.FullName,
                Department = employee.Department,
                Title = employee.Title,
                StartDate = input.StartDate,
                EndDate = input.EndDate,
                TotalHours = activities.Sum(a => a.HoursSpent),
                TotalActivities = activities.Count,
                CompletedTaskCount = tasks.Count(t => (t.Status == Entities.TaskStatus.Tamamlandi || t.Status == Entities.TaskStatus.Kapatildi)),
                PendingTaskCount = tasks.Count(t => t.Status == Entities.TaskStatus.Beklemede),
                InProgressTaskCount = tasks.Count(t => t.Status == Entities.TaskStatus.DevamEdiyor)
            };

            report.DailyActivities = activities
                .GroupBy(a => a.ActivityDate.Date)
                .Select(g => new DailyActivityDto
                {
                    Date = g.Key,
                    Hours = g.Sum(x => x.HoursSpent),
                    ActivityCount = g.Count(),
                    Descriptions = g.Select(x => x.Description).ToList()
                })
                .OrderBy(d => d.Date)
                .ToList();

            report.ProjectSummaries = activities
                .Where(a => a.ProjectId.HasValue)
                .GroupBy(a => new { a.ProjectId, Name = a.Project?.Name, Code = a.Project?.Code })
                .Select(g => new ProjectSummaryDto
                {
                    ProjectId = g.Key.ProjectId.Value,
                    ProjectName = g.Key.Name,
                    ProjectCode = g.Key.Code,
                    TotalHours = g.Sum(x => x.HoursSpent),
                    TaskCount = tasks.Count(t => t.ProjectId == g.Key.ProjectId),
                    CompletedTaskCount = tasks.Count(t => t.ProjectId == g.Key.ProjectId && (t.Status == Entities.TaskStatus.Tamamlandi || t.Status == Entities.TaskStatus.Kapatildi))
                })
                .ToList();

            report.TaskSummaries = tasks.Select(t => new TaskSummaryDto
            {
                TaskId = t.Id,
                Title = t.Title,
                Status = t.Status.ToString(),
                Priority = t.Priority.ToString(),
                DueDate = t.DueDate,
                ActualHours = t.ActualHours,
                CompletionPercentage = t.CompletionPercentage
            }).ToList();

            // Aktivite/Görev tipi bazlı kırılım: seçilen tarih aralığında TAMAMLANAN görevler, tiplerine göre
            report.TaskTypeBreakdown = tasks
                .Where(t => (t.Status == Entities.TaskStatus.Tamamlandi || t.Status == Entities.TaskStatus.Kapatildi) &&
                            t.CompletedDate.HasValue &&
                            t.CompletedDate.Value.Date >= input.StartDate.Date &&
                            t.CompletedDate.Value.Date <= input.EndDate.Date)
                .GroupBy(t => t.ActivityType)
                .Select(g => new TaskTypeSummaryDto
                {
                    Type = g.Key.HasValue ? ActivityTypeLabels[(int)g.Key.Value] : "Belirtilmemiş",
                    Count = g.Count(),
                    Hours = g.Sum(x => x.ActualHours)
                })
                .OrderByDescending(x => x.Count)
                .ToList();

            // Faaliyet (efor) tipi bazlı kırılım: seçilen aralıktaki ActivityLog kayıtları tiplerine göre
            report.ActivityTypeBreakdown = activities
                .GroupBy(a => string.IsNullOrWhiteSpace(a.ActivityType) ? "Diğer" : a.ActivityType)
                .Select(g => new TaskTypeSummaryDto
                {
                    Type = g.Key,
                    Count = g.Count(),
                    Hours = g.Sum(x => x.HoursSpent)
                })
                .OrderByDescending(x => x.Hours)
                .ToList();

            // V4: adım adım detaylı faaliyet kayıtları (alt başlık + tip + detay not)
            report.DetailedActivities = activities
                .OrderByDescending(a => a.ActivityDate)
                .Select(a => new DetailedActivityDto
                {
                    Date = a.ActivityDate,
                    SubHeading = a.ServiceRequest != null ? ("Talep: " + a.ServiceRequest.Title)
                                 : a.ActivitySubject?.Title ?? a.TaskItem?.Title ?? a.Project?.Name ?? "—",
                    ActivityType = string.IsNullOrWhiteSpace(a.ActivityType) ? "—" : a.ActivityType,
                    Detail = a.Description,
                    Hours = a.HoursSpent
                })
                .ToList();

            // V4: Tip bazlı BİRLEŞİK kırılım — görev (tamamlanan, tarih aralığında) + faaliyet (efor) tip tip birleştirilir.
            var taskByType = tasks
                .Where(t => (t.Status == Entities.TaskStatus.Tamamlandi || t.Status == Entities.TaskStatus.Kapatildi) &&
                            t.CompletedDate.HasValue &&
                            t.CompletedDate.Value.Date >= input.StartDate.Date &&
                            t.CompletedDate.Value.Date <= input.EndDate.Date)
                .GroupBy(t => t.ActivityType.HasValue ? ActivityTypeLabels[(int)t.ActivityType.Value] : "Belirtilmemiş")
                .ToDictionary(g => g.Key, g => new { Count = g.Count(), Hours = g.Sum(x => x.ActualHours) });

            var actByType = activities
                .GroupBy(a => string.IsNullOrWhiteSpace(a.ActivityType) ? "Belirtilmemiş" : a.ActivityType)
                .ToDictionary(g => g.Key, g => new { Count = g.Count(), Hours = g.Sum(x => x.HoursSpent) });

            var allTypeNames = taskByType.Keys.Union(actByType.Keys).OrderBy(x => x);
            report.CombinedTypeBreakdown = allTypeNames.Select(name => new TypeCombinedDto
            {
                Type = name,
                TaskCount = taskByType.TryGetValue(name, out var tv) ? tv.Count : 0,
                TaskHours = taskByType.TryGetValue(name, out var tv2) ? tv2.Hours : 0m,
                ActivityCount = actByType.TryGetValue(name, out var av) ? av.Count : 0,
                ActivityHours = actByType.TryGetValue(name, out var av2) ? av2.Hours : 0m
            })
            .OrderByDescending(x => x.TaskCount + x.ActivityCount)
            .ToList();

            // Doğal dil özet: hem görev hem faaliyeti tip tip belirtir ("3 Bakım görevi, 5 Bakım faaliyeti")
            var summaryParts = report.CombinedTypeBreakdown.Select(b =>
            {
                var segs = new System.Collections.Generic.List<string>();
                if (b.TaskCount > 0) segs.Add($"{b.TaskCount} {b.Type} görevi");
                if (b.ActivityCount > 0) segs.Add($"{b.ActivityCount} {b.Type} faaliyeti");
                return string.Join(" ve ", segs);
            }).Where(s => !string.IsNullOrEmpty(s));
            var breakdownText = report.CombinedTypeBreakdown.Any() ? string.Join("; ", summaryParts) : "kayıtlı iş bulunmuyor";
            report.SummaryText =
                $"{employee.FullName} — {input.StartDate:dd.MM.yyyy} - {input.EndDate:dd.MM.yyyy} tarihleri arasında: " +
                $"{breakdownText}. Toplam Harcanan Süre: {report.TotalHours:0.##} Saat ({report.TotalActivities} faaliyet kaydı).";

            return report;
        }

        public async Task<TeamReportDto> GetTeamReportAsync(GetReportInput input)
        {
            var employees = await _employeeRepository.GetAll().AsNoTracking()
                .Where(e => e.IsActive)
                .WhereIf(input.TeamId.HasValue, e => e.TeamId == input.TeamId.Value)
                .ToListAsync();
            var empIds = employees.Select(e => e.Id).ToList();

            // (N+1 giderildi) Tüm eforlar (tarih aralığı) + tüm görevler TEK sorguda çekilip bellekte gruplanır.
            var actByEmp = (await _activityRepository.GetAll().AsNoTracking()
                    .Where(a => empIds.Contains(a.EmployeeId)
                                && a.ActivityDate >= input.StartDate && a.ActivityDate <= input.EndDate)
                    .Select(a => new { a.EmployeeId, a.HoursSpent })
                    .ToListAsync())
                .GroupBy(a => a.EmployeeId)
                .ToDictionary(g => g.Key, g => new { Hours = g.Sum(x => x.HoursSpent), Count = g.Count() });

            var tskByEmp = (await _taskRepository.GetAll().AsNoTracking()
                    .Where(t => t.AssignedEmployeeId != null && empIds.Contains(t.AssignedEmployeeId.Value))
                    .Select(t => new { EmpId = t.AssignedEmployeeId.Value, t.Status })
                    .ToListAsync())
                .GroupBy(t => t.EmpId)
                .ToDictionary(g => g.Key, g => new
                {
                    Completed = g.Count(x => x.Status == Entities.TaskStatus.Tamamlandi || x.Status == Entities.TaskStatus.Kapatildi),
                    Pending = g.Count(x => x.Status == Entities.TaskStatus.Beklemede)
                });

            var report = new TeamReportDto { StartDate = input.StartDate, EndDate = input.EndDate };
            foreach (var emp in employees)
            {
                actByEmp.TryGetValue(emp.Id, out var a);
                tskByEmp.TryGetValue(emp.Id, out var t);
                report.EmployeeSummaries.Add(new EmployeeReportSummaryDto
                {
                    EmployeeId = emp.Id,
                    FullName = emp.FullName,
                    Department = emp.Department,
                    Title = emp.Title,
                    TotalHours = a?.Hours ?? 0,
                    TotalActivities = a?.Count ?? 0,
                    CompletedTasks = t?.Completed ?? 0,
                    PendingTasks = t?.Pending ?? 0
                });
            }

            return report;
        }
    }
}
