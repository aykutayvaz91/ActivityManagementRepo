using System;
using System.Collections.Generic;

namespace ActivityManagement.Reports.Dto
{
    public class PersonalReportDto
    {
        public long EmployeeId { get; set; }
        public string EmployeeName { get; set; }
        public string Department { get; set; }
        public string Title { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public decimal TotalHours { get; set; }
        public int TotalActivities { get; set; }
        public int CompletedTaskCount { get; set; }
        public int PendingTaskCount { get; set; }
        public int InProgressTaskCount { get; set; }

        public List<DailyActivityDto> DailyActivities { get; set; } = new List<DailyActivityDto>();
        public List<ProjectSummaryDto> ProjectSummaries { get; set; } = new List<ProjectSummaryDto>();
        public List<TaskSummaryDto> TaskSummaries { get; set; } = new List<TaskSummaryDto>();
    }

    public class DailyActivityDto
    {
        public DateTime Date { get; set; }
        public decimal Hours { get; set; }
        public int ActivityCount { get; set; }
        public List<string> Descriptions { get; set; } = new List<string>();
    }

    public class ProjectSummaryDto
    {
        public long ProjectId { get; set; }
        public string ProjectName { get; set; }
        public string ProjectCode { get; set; }
        public decimal TotalHours { get; set; }
        public int TaskCount { get; set; }
        public int CompletedTaskCount { get; set; }
    }

    public class TaskSummaryDto
    {
        public long TaskId { get; set; }
        public string Title { get; set; }
        public string Status { get; set; }
        public string Priority { get; set; }
        public DateTime? DueDate { get; set; }
        public decimal ActualHours { get; set; }
        public int CompletionPercentage { get; set; }
    }

    public class TeamReportDto
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public List<EmployeeReportSummaryDto> EmployeeSummaries { get; set; } = new List<EmployeeReportSummaryDto>();
    }

    public class EmployeeReportSummaryDto
    {
        public long EmployeeId { get; set; }
        public string FullName { get; set; }
        public string Department { get; set; }
        public string Title { get; set; }
        public decimal TotalHours { get; set; }
        public int CompletedTasks { get; set; }
        public int PendingTasks { get; set; }
        public int TotalActivities { get; set; }
    }

    public class GetReportInput
    {
        public long? EmployeeId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string ReportType { get; set; } = "personal"; // personal, team
    }
}
