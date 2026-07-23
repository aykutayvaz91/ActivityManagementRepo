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

        // Doğal dil özet ("Ahmet Yılmaz — 01.07.2026 - 31.07.2026 arasında ... tamamlamıştır. Toplam ... saat.")
        public string SummaryText { get; set; }

        public List<TaskTypeSummaryDto> TaskTypeBreakdown { get; set; } = new List<TaskTypeSummaryDto>();
        // Faaliyet (efor) kayıtlarının tiplerine göre kırılımı (ActivityLog.ActivityType)
        public List<TaskTypeSummaryDto> ActivityTypeBreakdown { get; set; } = new List<TaskTypeSummaryDto>();
        public List<DailyActivityDto> DailyActivities { get; set; } = new List<DailyActivityDto>();
        // V4: "Ne Yaptı?" — adım adım detaylı efor/faaliyet kayıtları (alt başlık + tip + detay not)
        public List<DetailedActivityDto> DetailedActivities { get; set; } = new List<DetailedActivityDto>();
        // V4: Tip bazlı BİRLEŞİK kırılım — "3 bakım görevi + 5 bakım faaliyeti"
        public List<TypeCombinedDto> CombinedTypeBreakdown { get; set; } = new List<TypeCombinedDto>();
        public List<ProjectSummaryDto> ProjectSummaries { get; set; } = new List<ProjectSummaryDto>();
        public List<TaskSummaryDto> TaskSummaries { get; set; } = new List<TaskSummaryDto>();
    }

    public class TaskTypeSummaryDto
    {
        public string Type { get; set; }
        public int Count { get; set; }
        public decimal Hours { get; set; }
    }

    // V4: Bir tipin görev ve faaliyet dağılımı birlikte
    public class TypeCombinedDto
    {
        public string Type { get; set; }
        public int TaskCount { get; set; }
        public decimal TaskHours { get; set; }
        public int ActivityCount { get; set; }
        public decimal ActivityHours { get; set; }
    }

    public class DailyActivityDto
    {
        public DateTime Date { get; set; }
        public decimal Hours { get; set; }
        public int ActivityCount { get; set; }
        public List<string> Descriptions { get; set; } = new List<string>();
    }

    // V4: Tek satır = tek efor kaydı (adım adım)
    public class DetailedActivityDto
    {
        public DateTime Date { get; set; }
        public string SubHeading { get; set; }   // Alt başlık (faaliyet konusu / görev / proje)
        public string ActivityType { get; set; } // Faaliyet tipi
        public string Detail { get; set; }        // Detay not (açıklama)
        public decimal Hours { get; set; }
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
        public long? TeamId { get; set; } // ekip raporunu takımla sınırlamak için (TakımLideri)
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string ReportType { get; set; } = "personal"; // personal, team
    }
}
