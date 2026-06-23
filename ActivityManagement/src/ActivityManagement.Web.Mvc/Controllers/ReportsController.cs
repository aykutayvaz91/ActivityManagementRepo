using System;
using System.Threading.Tasks;
using Abp.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Mvc;
using ActivityManagement.Authorization;
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

        public IActionResult Index()
        {
            return View();
        }

        public async Task<IActionResult> Personal(long? employeeId, DateTime? startDate, DateTime? endDate)
        {
            ViewBag.Employees = (await _employeeAppService.GetAllListAsync()).Items;

            if (!employeeId.HasValue) return View("PersonalForm");

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
            var input = new GetReportInput
            {
                StartDate = startDate ?? DateTime.Today.AddDays(-30),
                EndDate = endDate ?? DateTime.Today
            };

            var report = await _reportAppService.GetTeamReportAsync(input);
            return View("TeamReport", report);
        }
    }
}
