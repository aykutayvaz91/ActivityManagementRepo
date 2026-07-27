using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using ActivityManagement.Employees;
using ActivityManagement.Reports;
using ActivityManagement.Reports.Dto;

namespace ActivityManagement.Web.Controllers
{
    public class ReportsController : ActivityManagementControllerBase
    {
        private readonly IReportAppService _reportAppService;
        private readonly IEmployeeAppService _employeeAppService;

        public ReportsController(IReportAppService reportAppService, IEmployeeAppService employeeAppService)
        {
            _reportAppService = reportAppService;
            _employeeAppService = employeeAppService;
        }

        // Manager, rapor/görünürlükte Admin gibi TÜM takım ve kişileri kapsar (yalnız admin-panel ayarlarını yapamaz).
        private bool SeesAll() => User.IsInRole("Admin") || User.IsInRole("Manager");
        // Ekip raporu çekebilen / başkasının raporunu seçebilen roller: Admin, Manager, TakımLideri.
        private bool IsManager() => SeesAll() || User.IsInRole("TakımLideri");
        private long? CurrentEmployeeId()
        {
            var c = User.FindFirst("EmployeeId")?.Value;
            return long.TryParse(c, out var id) ? id : (long?)null;
        }
        private async Task<long?> CurrentTeamIdAsync()
        {
            var e = CurrentEmployeeId();
            return e.HasValue ? (await _employeeAppService.GetAsync(e.Value)).TeamId : null;
        }

        // Rapor için seçilebilecek personeller (Admin: hepsi, Lider: kendi takımı, Uzman: sadece kendisi)
        private async Task<System.Collections.Generic.List<ActivityManagement.Employees.Dto.EmployeeDto>> ScopedEmployeesAsync()
        {
            var all = (await _employeeAppService.GetAllListAsync()).Items;
            if (SeesAll()) return all.ToList();                                   // Admin/Manager → tüm personel
            if (IsManager()) { var t = await CurrentTeamIdAsync(); return all.Where(e => e.TeamId == t).ToList(); } // TakımLideri → kendi takımı
            var myId = CurrentEmployeeId();
            return all.Where(e => e.Id == myId).ToList();
        }

        public IActionResult Index()
        {
            return View();
        }

        public async Task<IActionResult> Personal(long? employeeId, DateTime? startDate, DateTime? endDate)
        {
            var g = EnsurePageAccess("Reports"); if (g != null) return g;
            var scoped = await ScopedEmployeesAsync();
            ViewBag.Employees = scoped;

            // Uzman yalnızca kendi raporunu görür
            if (!IsManager()) employeeId = CurrentEmployeeId();

            if (!employeeId.HasValue) return View("PersonalForm");

            // Yetki doğrulama (URL ile kapsam dışı kişi çekmeyi engelle)
            if (!SeesAll() && scoped.All(e => e.Id != employeeId.Value))
            {
                TempData["Uyari"] = "Bu personelin raporunu görüntüleme yetkiniz yok.";
                return View("PersonalForm");
            }

            var input = new GetReportInput
            {
                EmployeeId = employeeId,
                StartDate = startDate ?? DateTime.Today.AddDays(-30),
                EndDate = endDate ?? DateTime.Today
            };

            var report = await _reportAppService.GetPersonalReportAsync(input);
            return View("PersonalReport", report);
        }

        public async Task<IActionResult> Team(DateTime? startDate, DateTime? endDate)
        {
            var g = EnsurePageAccess("Reports"); if (g != null) return g;
            // Ekip raporu yalnızca Admin/TakımLideri; Uzman erişemez
            if (!IsManager())
            {
                TempData["Uyari"] = "Ekip raporu görüntüleme yetkiniz yok. Kişisel raporunuzu görebilirsiniz.";
                return RedirectToAction("Personal");
            }

            var input = new GetReportInput
            {
                StartDate = startDate ?? DateTime.Today.AddDays(-30),
                EndDate = endDate ?? DateTime.Today,
                TeamId = SeesAll() ? null : await CurrentTeamIdAsync() // Admin/Manager → tüm takımlar; Lider → kendi takımı
            };

            var report = await _reportAppService.GetTeamReportAsync(input);
            return View("TeamReport", report);
        }

        public async Task<IActionResult> ExportPersonalExcel(long employeeId, DateTime? startDate, DateTime? endDate)
        {
            if (!IsManager()) employeeId = CurrentEmployeeId() ?? employeeId;
            // Personel seçilmeden export → 500 yerine formu geri göster (admin employeeId=0 edge'i)
            if (employeeId <= 0)
            {
                TempData["Uyari"] = "Lütfen önce bir personel seçin.";
                return RedirectToAction("Personal");
            }
            if (!SeesAll())
            {
                var scoped = await ScopedEmployeesAsync();
                if (scoped.All(e => e.Id != employeeId)) return AccessDeniedRedirect("/Reports/Personal");
            }
            var input = new GetReportInput
            {
                EmployeeId = employeeId,
                StartDate = startDate ?? DateTime.Today.AddDays(-30),
                EndDate = endDate ?? DateTime.Today
            };
            var r = await _reportAppService.GetPersonalReportAsync(input);

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Kişisel Rapor");
            ws.Cell(1, 1).Value = "Kişisel Rapor";
            ws.Cell(2, 1).Value = "Personel"; ws.Cell(2, 2).Value = r.EmployeeName;
            ws.Cell(3, 1).Value = "Dönem"; ws.Cell(3, 2).Value = $"{r.StartDate:dd.MM.yyyy} - {r.EndDate:dd.MM.yyyy}";
            ws.Cell(4, 1).Value = "Özet"; ws.Cell(4, 2).Value = r.SummaryText;
            ws.Cell(6, 1).Value = "İş Tipi"; ws.Cell(6, 2).Value = "Adet"; ws.Cell(6, 3).Value = "Saat";
            int row = 7;
            foreach (var b in r.TaskTypeBreakdown)
            {
                ws.Cell(row, 1).Value = b.Type; ws.Cell(row, 2).Value = b.Count; ws.Cell(row, 3).Value = b.Hours; row++;
            }
            row++;
            ws.Cell(row, 1).Value = "Tarih"; ws.Cell(row, 2).Value = "Faaliyet"; ws.Cell(row, 3).Value = "Saat"; row++;
            foreach (var d in r.DailyActivities)
            {
                ws.Cell(row, 1).Value = d.Date.ToString("dd.MM.yyyy"); ws.Cell(row, 2).Value = d.ActivityCount; ws.Cell(row, 3).Value = d.Hours; row++;
            }
            ws.Columns().AdjustToContents();
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"KisiselRapor_{r.EmployeeName}_{DateTime.Today:yyyyMMdd}.xlsx");
        }

        public async Task<IActionResult> ExportTeamExcel(DateTime? startDate, DateTime? endDate)
        {
            if (!IsManager()) return AccessDeniedRedirect("/Reports/Personal");
            var input = new GetReportInput
            {
                StartDate = startDate ?? DateTime.Today.AddDays(-30),
                EndDate = endDate ?? DateTime.Today,
                TeamId = SeesAll() ? null : await CurrentTeamIdAsync()
            };
            var r = await _reportAppService.GetTeamReportAsync(input);

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Ekip Raporu");
            ws.Cell(1, 1).Value = $"Ekip Raporu ({r.StartDate:dd.MM.yyyy} - {r.EndDate:dd.MM.yyyy})";
            var headers = new[] { "Personel", "Departman", "Ünvan", "Toplam Saat", "Faaliyet", "Tamamlanan Görev", "Bekleyen Görev" };
            for (int i = 0; i < headers.Length; i++) ws.Cell(3, i + 1).Value = headers[i];
            int row = 4;
            foreach (var e in r.EmployeeSummaries)
            {
                ws.Cell(row, 1).Value = e.FullName;
                ws.Cell(row, 2).Value = e.Department;
                ws.Cell(row, 3).Value = e.Title;
                ws.Cell(row, 4).Value = e.TotalHours;
                ws.Cell(row, 5).Value = e.TotalActivities;
                ws.Cell(row, 6).Value = e.CompletedTasks;
                ws.Cell(row, 7).Value = e.PendingTasks;
                row++;
            }
            ws.Columns().AdjustToContents();
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"EkipRaporu_{DateTime.Today:yyyyMMdd}.xlsx");
        }
    }
}
