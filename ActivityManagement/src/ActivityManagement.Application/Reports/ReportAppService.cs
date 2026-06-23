using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abp.Application.Services;
using Abp.Authorization;
using Abp.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using ActivityManagement.Authorization;
using ActivityManagement.Entities;
using ActivityManagement.Reports.Dto;

namespace ActivityManagement.Reports
{
    [AbpAuthorize(ActivityManagementPermissions.Reports.Default)]
    public class ReportAppService : ActivityManagementAppServiceBase, IReportAppService
    {
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

        [AbpAuthorize(ActivityManagementPermissions.Reports.Personal)]
        public async Task<PersonalReportDto> GetPersonalReportAsync(GetReportInput input)
        {
            var employee = await _employeeRepository.GetAsync(input.EmployeeId.Value);

            var activities = await _activityRepository.GetAll()
                .Include(a => a.Project)
                .Include(a => a.TaskItem)
                .Where(a => a.EmployeeId == input.EmployeeId.Value &&
                            a.ActivityDate >= input.StartDate &&
                            a.ActivityDate <= input.EndDate)
                .ToListAsync();

            var tasks = await _taskRepository.GetAll()
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
                CompletedTaskCount = tasks.Count(t => t.Status == Entities.TaskStatus.Tamamlandi),
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
                    CompletedTaskCount = tasks.Count(t => t.ProjectId == g.Key.ProjectId && t.Status == Entities.TaskStatus.Tamamlandi)
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

            return report;
        }

        [AbpAuthorize(ActivityManagementPermissions.Reports.Team)]
        public async Task<TeamReportDto> GetTeamReportAsync(GetReportInput input)
        {
            var employees = await _employeeRepository.GetAll()
                .Where(e => e.IsActive)
                .ToListAsync();

            var report = new TeamReportDto
            {
                StartDate = input.StartDate,
                EndDate = input.EndDate
            };

            foreach (var emp in employees)
            {
                var activities = await _activityRepository.GetAll()
                    .Where(a => a.EmployeeId == emp.Id &&
                                a.ActivityDate >= input.StartDate && a.ActivityDate <= input.EndDate)
                    .ToListAsync();

                var tasks = await _taskRepository.GetAll()
                    .Where(t => t.AssignedEmployeeId == emp.Id)
                    .ToListAsync();

                report.EmployeeSummaries.Add(new EmployeeReportSummaryDto
                {
                    EmployeeId = emp.Id,
                    FullName = emp.FullName,
                    Department = emp.Department,
                    Title = emp.Title,
                    TotalHours = activities.Sum(a => a.HoursSpent),
                    TotalActivities = activities.Count,
                    CompletedTasks = tasks.Count(t => t.Status == Entities.TaskStatus.Tamamlandi),
                    PendingTasks = tasks.Count(t => t.Status == Entities.TaskStatus.Beklemede)
                });
            }

            return report;
        }
    }
}
