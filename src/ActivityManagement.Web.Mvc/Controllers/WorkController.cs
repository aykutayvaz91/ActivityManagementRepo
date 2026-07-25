using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ActivityManagement.Activities;
using ActivityManagement.Activities.Dto;
using ActivityManagement.Entities;
using ActivityManagement.ServiceRequests;
using ActivityManagement.ServiceRequests.Dto;
using ActivityManagement.Tasks;
using ActivityManagement.Web.Models;

namespace ActivityManagement.Web.Controllers
{
    // "İşlerim" hub'ının Genel Bakış ekranı: kişinin açık Görev + Talep + Faaliyet'lerini
    // tek listede, SLA/önem sırasına göre birleştirir. (Görevlerim/Pano/Faaliyetler ayrı ekranlar.)
    public class WorkController : ActivityManagementControllerBase
    {
        private readonly ITaskItemAppService _taskAppService;
        private readonly IServiceRequestAppService _requestAppService;
        private readonly IActivitySubjectAppService _subjectAppService;

        public WorkController(
            ITaskItemAppService taskAppService,
            IServiceRequestAppService requestAppService,
            IActivitySubjectAppService subjectAppService)
        {
            _taskAppService = taskAppService;
            _requestAppService = requestAppService;
            _subjectAppService = subjectAppService;
        }

        private long? CurrentEmployeeId()
        {
            var c = User.FindFirst("EmployeeId")?.Value;
            return long.TryParse(c, out var id) ? id : (long?)null;
        }

        // İşlerim → Genel Bakış
        public async Task<IActionResult> Index()
        {
            var g = EnsurePageAccess("Work"); if (g != null) return g;
            var rows = new List<WorkItemRow>();
            var myId = CurrentEmployeeId();

            if (!myId.HasValue)
            {
                // Personel kaydı olmayan hesap (ör. Sistem Yöneticisi kendi kimliği) → boş durum + yönlendirme.
                ViewBag.NoEmployee = true;
                return View(rows);
            }

            try
            {
                // 1) Açık görevlerim (Tamamlandı/Kapatıldı/İptal hariç — Kapatıldı arşivdir, açık işte görünmez)
                var tasks = (await _taskAppService.GetEmployeeTasksAsync(myId.Value)).Items
                    .Where(t => t.Status != Entities.TaskStatus.Tamamlandi
                                && t.Status != Entities.TaskStatus.Kapatildi
                                && t.Status != Entities.TaskStatus.Iptal);
                foreach (var t in tasks)
                {
                    rows.Add(new WorkItemRow
                    {
                        Kind = "Görev", KindIcon = "fa-clipboard-list", KindColor = "primary",
                        Id = t.Id, Title = t.Title, Link = $"/Tasks/Detail/{t.Id}",
                        StatusText = t.StatusText, StatusColor = StatusColorForTask(t.Status),
                        Context = t.ProjectName ?? t.CategoryName ?? t.SubCategoryName,
                        DueDate = t.DueDate, PriorityScore = t.PriorityScore, Percentage = t.CompletionPercentage,
                        IsOverdue = t.DueDate.HasValue && t.DueDate.Value.Date < DateTime.Today && t.Status != Entities.TaskStatus.Tamamlandi
                    });
                }

                // 2) Açık taleplerim (bana atanan)
                var reqs = await _requestAppService.GetAllAsync(new GetServiceRequestsInput { MineOnly = true, OnlyOpen = true });
                foreach (var r in reqs)
                {
                    rows.Add(new WorkItemRow
                    {
                        Kind = "Talep", KindIcon = "fa-inbox", KindColor = "info",
                        Id = r.Id, Title = r.Title, Link = $"/Requests/Detail/{r.Id}",
                        StatusText = r.StatusText, StatusColor = StatusColorForRequest(r.Status),
                        Context = r.SourceText + (string.IsNullOrEmpty(r.ProjectName) ? "" : " · " + r.ProjectName),
                        DueDate = r.DueDate, PriorityScore = r.PriorityScore, Percentage = r.CompletionPercentage,
                        IsOverdue = r.IsOverdue
                    });
                }

                // 3) Faaliyet konularım (bana atanan, aktif)
                var subjects = await _subjectAppService.GetAllAsync(new GetActivitySubjectsInput { AssignedEmployeeId = myId.Value, OnlyActive = true });
                foreach (var s in subjects)
                {
                    rows.Add(new WorkItemRow
                    {
                        Kind = "Faaliyet", KindIcon = "fa-clipboard-check", KindColor = "success",
                        Id = s.Id, Title = s.Title, Link = $"/Activities/Detail/{s.Id}",
                        StatusText = "Aktif", StatusColor = "success",
                        Context = s.ProjectName ?? s.CategoryName,
                        DueDate = null, PriorityScore = 5, Percentage = 0, IsOverdue = false
                    });
                }
            }
            catch (Exception ex)
            {
                ActivityManagement.Logging.ErrorLog.Write(ex, "Work/Index");
            }

            var ordered = rows
                .OrderByDescending(x => x.IsOverdue)
                .ThenBy(x => x.SortDue)
                .ThenByDescending(x => x.PriorityScore)
                .ToList();

            ViewBag.OpenTaskCount = ordered.Count(x => x.Kind == "Görev");
            ViewBag.OpenReqCount = ordered.Count(x => x.Kind == "Talep");
            ViewBag.SubjectCount = ordered.Count(x => x.Kind == "Faaliyet");
            ViewBag.OverdueCount = ordered.Count(x => x.IsOverdue);
            return View(ordered);
        }

        private static string StatusColorForTask(Entities.TaskStatus s) =>
            s == Entities.TaskStatus.Beklemede ? "secondary" :
            s == Entities.TaskStatus.DevamEdiyor ? "primary" :
            s == Entities.TaskStatus.Tamamlandi ? "success" :
            s == Entities.TaskStatus.Ertelendi ? "warning" : "danger";

        private static string StatusColorForRequest(RequestStatus s) =>
            s == RequestStatus.Yeni ? "secondary" : s == RequestStatus.Atandi ? "info" :
            s == RequestStatus.DevamEdiyor ? "primary" : s == RequestStatus.Beklemede ? "warning" :
            s == RequestStatus.Cozuldu ? "success" : s == RequestStatus.Kapandi ? "dark" : "danger";
    }
}
