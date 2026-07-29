using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ActivityManagement.Categories;
using ActivityManagement.Categories.Dto;
using ActivityManagement.SystemSettings;
using ActivityManagement.SystemSettings.Dto;
using ActivityManagement.Employees;
using ActivityManagement.Employees.Dto;
using ActivityManagement.Projects;
using ActivityManagement.Projects.Dto;
using ActivityManagement.Tasks;
using ActivityManagement.Tasks.Dto;
using ActivityManagement.Teams;
using ActivityManagement.Teams.Dto;
using ActivityManagement.Workflow;

namespace ActivityManagement.Web.Controllers
{
    // Kategori/Alt Kategori yönetimi Admin+TakımLideri'ye açık; Rol ve Durum Yönetimi
    // action seviyesinde ayrıca sadece Admin'e kısıtlanıyor.
    [Authorize(Roles = "Admin,TakımLideri")]
    public class AdminController : ActivityManagementControllerBase
    {
        private readonly IEmployeeAppService _employeeAppService;
        private readonly IProjectAppService _projectAppService;
        private readonly ITaskItemAppService _taskAppService;
        private readonly IWorkflowStatusAppService _workflowStatusAppService;
        private readonly ICategoryAppService _categoryAppService;
        private readonly ITeamAppService _teamAppService;
        private readonly IEmailSettingsAppService _emailSettingsAppService;
        private readonly ActivityManagement.Auditing.IAuditLogAppService _auditLogAppService;
        private readonly ActivityManagement.Responsibilities.ISubCategoryResponsibilityAppService _responsibilityAppService;
        private readonly ActivityManagement.Activities.IActivityTypeAppService _activityTypeAppService;
        private readonly ActivityManagement.Theming.IThemeSettingsAppService _themeAppService;
        private readonly ActivityManagement.Authorization.IAccessControlAppService _accessControlAppService;
        private readonly ActivityManagement.SystemSettings.IIntegrationSettingsAppService _integrationAppService;
        private readonly Microsoft.AspNetCore.Hosting.IWebHostEnvironment _env;

        public AdminController(
            IEmployeeAppService employeeAppService,
            IProjectAppService projectAppService,
            ITaskItemAppService taskAppService,
            IWorkflowStatusAppService workflowStatusAppService,
            ICategoryAppService categoryAppService,
            ITeamAppService teamAppService,
            IEmailSettingsAppService emailSettingsAppService,
            ActivityManagement.Auditing.IAuditLogAppService auditLogAppService,
            ActivityManagement.Responsibilities.ISubCategoryResponsibilityAppService responsibilityAppService,
            ActivityManagement.Activities.IActivityTypeAppService activityTypeAppService,
            ActivityManagement.Theming.IThemeSettingsAppService themeAppService,
            ActivityManagement.Authorization.IAccessControlAppService accessControlAppService,
            ActivityManagement.SystemSettings.IIntegrationSettingsAppService integrationAppService,
            Microsoft.AspNetCore.Hosting.IWebHostEnvironment env)
        {
            _employeeAppService = employeeAppService;
            _projectAppService = projectAppService;
            _taskAppService = taskAppService;
            _workflowStatusAppService = workflowStatusAppService;
            _categoryAppService = categoryAppService;
            _teamAppService = teamAppService;
            _emailSettingsAppService = emailSettingsAppService;
            _auditLogAppService = auditLogAppService;
            _responsibilityAppService = responsibilityAppService;
            _activityTypeAppService = activityTypeAppService;
            _themeAppService = themeAppService;
            _accessControlAppService = accessControlAppService;
            _integrationAppService = integrationAppService;
            _env = env;
        }

        // FAZ 2 — Entegrasyon Ayarları (webhook + pull kaynakları) — sadece Admin
        public async Task<IActionResult> Integration()
        {
            if (!IsAdmin()) return AccessDeniedRedirect();
            var dto = await _integrationAppService.GetAsync();
            return View(dto);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveIntegrationGeneral(string inboundApiKey, bool syncEnabled, int intervalMinutes, bool clearInboundKey = false)
        {
            if (!IsAdmin()) return AccessDeniedRedirect();
            try
            {
                await _integrationAppService.SaveGeneralAsync(inboundApiKey, syncEnabled, intervalMinutes, clearInboundKey);
                TempData["Success"] = "Entegrasyon genel ayarları kaydedildi.";
            }
            catch (Abp.UI.UserFriendlyException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction("Integration");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveIntegrationSource(int id, bool enabled, string baseUrl, string apiKey,
            string authHeader, string authScheme, string filter, int initialLookbackDays, string userEmail = null,
            bool detailSyncEnabled = false, bool writeBackEnabled = false)
        {
            if (!IsAdmin()) return AccessDeniedRedirect();
            try
            {
                await _integrationAppService.SaveSourceAsync(id, enabled, baseUrl, apiKey, authHeader, authScheme, filter, initialLookbackDays, userEmail, detailSyncEnabled, writeBackEnabled);
                TempData["Success"] = "Kaynak ayarı kaydedildi.";
            }
            catch (Abp.UI.UserFriendlyException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction("Integration");
        }

        // V4/R2: Rol × Sayfa Erişim Matrisi (Admin)
        public async Task<IActionResult> RoleAccess()
        {
            if (!IsAdmin()) return AccessDeniedRedirect();
            ViewBag.Matrix = await _accessControlAppService.GetMatrixAsync();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveRoleAccess(string[] allow)
        {
            if (!IsAdmin()) return AccessDeniedRedirect();
            try
            {
                var map = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>();
                foreach (var item in allow ?? System.Array.Empty<string>())
                {
                    var parts = item.Split('|');
                    if (parts.Length != 2) continue;
                    if (!map.TryGetValue(parts[0], out var list)) { list = new System.Collections.Generic.List<string>(); map[parts[0]] = list; }
                    list.Add(parts[1]);
                }
                // İşaretlenmemiş roller de map'te boş listeyle yer alsın ki erişimleri kaldırılabilsin
                foreach (var r in (await _accessControlAppService.GetRolesAsync()))
                    if (!map.ContainsKey(r.Name)) map[r.Name] = new System.Collections.Generic.List<string>();

                await _accessControlAppService.SaveMatrixAsync(map);
                TempData["Success"] = "Rol erişim matrisi güncellendi.";
            }
            catch (Abp.UI.UserFriendlyException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction("RoleAccess");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateRole(string name, string displayName)
        {
            if (!IsAdmin()) return AccessDeniedRedirect();
            try { await _accessControlAppService.CreateRoleAsync(name, displayName); TempData["Success"] = "Rol eklendi."; }
            catch (Abp.UI.UserFriendlyException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction("RoleAccess");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteRole(int id)
        {
            if (!IsAdmin()) return AccessDeniedRedirect();
            try { await _accessControlAppService.DeleteRoleAsync(id); TempData["Success"] = "Rol silindi."; }
            catch (Abp.UI.UserFriendlyException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction("RoleAccess");
        }

        // V4: Tema ayarları (Admin) — ana renk + logo + marka adı
        public async Task<IActionResult> Theme()
        {
            if (!IsAdmin()) return AccessDeniedRedirect();
            ViewBag.Theme = await _themeAppService.GetAsync();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveTheme(string primaryColor, string brandName, bool useTeamNameAsBrand, Microsoft.AspNetCore.Http.IFormFile logoFile)
        {
            if (!IsAdmin()) return AccessDeniedRedirect();
            try
            {
                var dto = await _themeAppService.GetAsync();
                dto.PrimaryColor = string.IsNullOrWhiteSpace(primaryColor) ? dto.PrimaryColor : primaryColor;
                dto.BrandName = string.IsNullOrWhiteSpace(brandName) ? dto.BrandName : brandName;
                dto.UseTeamNameAsBrand = useTeamNameAsBrand;

                if (logoFile != null && logoFile.Length > 0)
                {
                    // GÜVENLİK: logo yalnızca güvenli raster görsel (svg reddedilir — script taşıyabilir → XSS)
                    if (!ActivityManagement.Web.Helpers.UploadValidator.IsInlineSafe(logoFile.FileName))
                    { TempData["Error"] = "Logo yalnızca PNG/JPG/GIF/WEBP olabilir."; return RedirectToAction("Theme"); }
                    var uploads = System.IO.Path.Combine(_env.WebRootPath, "uploads", "brand");
                    System.IO.Directory.CreateDirectory(uploads);
                    var ext = System.IO.Path.GetExtension(logoFile.FileName);
                    var fileName = "logo" + ext;
                    var full = System.IO.Path.Combine(uploads, fileName);
                    using (var fs = new System.IO.FileStream(full, System.IO.FileMode.Create))
                        await logoFile.CopyToAsync(fs);
                    dto.LogoUrl = "/uploads/brand/" + fileName + "?v=" + System.DateTime.Now.Ticks;
                }

                await _themeAppService.UpdateAsync(dto);
                TempData["Success"] = "Tema ayarları güncellendi.";
            }
            catch (Abp.UI.UserFriendlyException ex) { TempData["Error"] = ex.Message; }
            catch (System.Exception ex) { ActivityManagement.Logging.ErrorLog.Write(ex, "Admin/SaveTheme"); TempData["Error"] = "Tema kaydedilirken hata oluştu."; }
            return RedirectToAction("Theme");
        }

        // V4: Faaliyet Tipi yönetimi (Admin)
        public async Task<IActionResult> ActivityTypes()
        {
            if (!IsAdmin()) return AccessDeniedRedirect();
            ViewBag.Types = await _activityTypeAppService.GetAllAsync(onlyActive: false);
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveActivityType(ActivityManagement.Activities.Dto.CreateUpdateActivityTypeDto input)
        {
            try
            {
                if (input.Id > 0) { await _activityTypeAppService.UpdateAsync(input); TempData["Success"] = "Faaliyet tipi güncellendi."; }
                else { await _activityTypeAppService.CreateAsync(input); TempData["Success"] = "Faaliyet tipi eklendi."; }
            }
            catch (Abp.UI.UserFriendlyException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction("ActivityTypes");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteActivityType(int id)
        {
            try
            {
                await _activityTypeAppService.DeleteAsync(id);
                TempData["Success"] = "Faaliyet tipi silindi.";
            }
            catch (Abp.UI.UserFriendlyException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction("ActivityTypes");
        }

        private long? CurrentEmployeeIdClaim()
        {
            var c = User.FindFirst("EmployeeId")?.Value;
            return long.TryParse(c, out var id) ? id : (long?)null;
        }

        public async Task<IActionResult> Index()
        {
            var g = EnsurePageAccess("Admin"); if (g != null) return g;

            // "Login as" — admin hangi kişi olarak işlem yapıyor + geçiş için personel listesi
            var allEmployees = (await _employeeAppService.GetAllListAsync()).Items;
            ViewBag.ActAsEmployees = allEmployees;
            var actId = CurrentEmployeeIdClaim();
            ViewBag.ActingAsId = actId;
            ViewBag.ActingAsName = actId.HasValue ? allEmployees.FirstOrDefault(e => e.Id == actId.Value)?.FullName : null;

            var emps = await _employeeAppService.GetAllAsync(new GetEmployeesInput { MaxResultCount = 1000 });
            var prjs = await _projectAppService.GetAllAsync(new GetProjectsInput { MaxResultCount = 1000 });
            var tasks = await _taskAppService.GetAllAsync(new GetTasksInput { MaxResultCount = 1000 });

            ViewBag.EmployeeCount = emps.TotalCount;
            ViewBag.ProjectCount = prjs.TotalCount;
            ViewBag.TaskCount = tasks.TotalCount;
            ViewBag.AdminCount = emps.Items.Count(e => e.AppRole == "Admin");
            return View();
        }

        private bool IsAdmin() => User.IsInRole("Admin");

        // "Login as" — admin, seçtiği personel olarak işlem yapmak üzere oturum cookie'sini yeniden imzalar
        // (rol Admin kalır; EmployeeId claim'i seçilen kişiye ayarlanır). Kişisel akışlar (efor, günlük efor,
        // görevlerim, kişi kartı) bu kişi üzerinden yürür.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ActAs(long employeeId)
        {
            if (!IsAdmin()) return AccessDeniedRedirect();
            // Doğrudan id ile çöz (Sistem Yöneticisi dropdown'da olmasa da kendine dönüş çalışsın).
            ActivityManagement.Employees.Dto.EmployeeDto emp;
            try { emp = await _employeeAppService.GetAsync(employeeId); } catch { emp = null; }
            if (emp == null || emp.Id == 0)
            {
                TempData["Uyari"] = "Personel bulunamadı.";
                return RedirectToAction("Index");
            }
            var adminEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? User.Identity?.Name ?? "admin";
            // Admin'in kendi (Sistem Yöneticisi) personel id'si — mevcut claim'den
            var ownId = User.FindFirst("AdminOwnEmployeeId")?.Value;
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, adminEmail),
                new Claim(ClaimTypes.Email, adminEmail),
                new Claim(ClaimTypes.Role, "Admin"),
                new Claim("IsAdmin", "true"),
                new Claim("EmployeeId", employeeId.ToString()),
                new Claim("ActingAsName", emp.FullName ?? "")
            };
            if (!string.IsNullOrEmpty(ownId))
                claims.Add(new Claim("AdminOwnEmployeeId", ownId));
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity),
                new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8) });
            TempData["Success"] = $"Artık '{emp.FullName}' olarak işlem yapıyorsunuz. (Görev/efor bu kişi adına kaydedilir.)";
            return RedirectToAction("Index");
        }

        // Kendine (Sistem Yöneticisi) dön — login-as'i sıfırlar. (Sistem Yöneticisi dropdown'da olmadığından ayrı buton.)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReturnToSelf()
        {
            if (!IsAdmin()) return AccessDeniedRedirect();
            var ownId = User.FindFirst("AdminOwnEmployeeId")?.Value;
            if (long.TryParse(ownId, out var id)) return await ActAs(id);
            TempData["Uyari"] = "Kendi (Sistem Yöneticisi) kaydınız bulunamadı.";
            return RedirectToAction("Index");
        }

        // Rol yönetimi - sadece Admin
        public async Task<IActionResult> Roles()
        {
            if (!IsAdmin()) return AccessDeniedRedirect();
            var emps = await _employeeAppService.GetAllAsync(new GetEmployeesInput { MaxResultCount = 1000 });
            ViewBag.Roles = await _accessControlAppService.GetRolesAsync(); // dinamik roller (özel roller dahil)
            return View(emps.Items);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetRole(long id, string appRole)
        {
            if (!IsAdmin()) return AccessDeniedRedirect();
            await _employeeAppService.UpdateRoleAsync(id, appRole);
            TempData["Success"] = "Rol güncellendi.";
            return RedirectToAction("Roles");
        }

        // Durum (workflow) yönetimi - sadece Admin
        public async Task<IActionResult> Statuses()
        {
            if (!IsAdmin()) return AccessDeniedRedirect();
            var list = await _workflowStatusAppService.GetAllAsync(false);
            return View(list);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveStatus(CreateUpdateWorkflowStatusDto input)
        {
            if (!IsAdmin()) return AccessDeniedRedirect();
            if (input.Id > 0) await _workflowStatusAppService.UpdateAsync(input);
            else await _workflowStatusAppService.CreateAsync(input);
            TempData["Success"] = "Durum kaydedildi.";
            return RedirectToAction("Statuses");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteStatus(int id)
        {
            if (!IsAdmin()) return AccessDeniedRedirect();
            await _workflowStatusAppService.DeleteAsync(id);
            TempData["Success"] = "Durum silindi.";
            return RedirectToAction("Statuses");
        }

        private long? CurrentEmployeeId()
        {
            var c = User.FindFirst("EmployeeId")?.Value;
            return long.TryParse(c, out var id) ? id : (long?)null;
        }

        // Kategori / Alt Kategori yönetimi
        public async Task<IActionResult> Categories()
        {
            var categories = await _categoryAppService.GetAllAsync();
            var emps = await _employeeAppService.GetAllAsync(new GetEmployeesInput { MaxResultCount = 1000 });
            ViewBag.Employees = emps.Items.OrderBy(e => e.FullName).ToList();
            ViewBag.Teams = await _teamAppService.GetAllAsync();

            if (!IsAdmin())
            {
                var myEmpId = CurrentEmployeeId();
                var myTeamId = myEmpId.HasValue
                    ? emps.Items.FirstOrDefault(e => e.Id == myEmpId.Value)?.TeamId
                    : null;
                ViewBag.MyTeamId = myTeamId;
            }

            return View(categories);
        }

        // V4: Admin ana kategori ekler/günceller (Id=0 ise yeni).
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveCategory(CreateUpdateCategoryDto input)
        {
            try
            {
                if (input.Id > 0) { await _categoryAppService.UpdateAsync(input); TempData["Success"] = "Ana kategori güncellendi."; }
                else { await _categoryAppService.CreateAsync(input); TempData["Success"] = "Ana kategori eklendi."; }
            }
            catch (Abp.UI.UserFriendlyException ex)
            {
                TempData["Error"] = ex.Message;
            }
            return RedirectToAction("Categories");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCategory(long id)
        {
            try
            {
                await _categoryAppService.DeleteAsync(id);
                TempData["Success"] = "Ana kategori silindi.";
            }
            catch (Abp.UI.UserFriendlyException ex)
            {
                TempData["Error"] = ex.Message;
            }
            return RedirectToAction("Categories");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveSubCategory(CreateUpdateSubCategoryDto input)
        {
            try
            {
                if (input.Id > 0) await _categoryAppService.UpdateSubCategoryAsync(input);
                else await _categoryAppService.CreateSubCategoryAsync(input);
                TempData["Success"] = "Alt kategori kaydedildi.";
            }
            catch (Abp.UI.UserFriendlyException ex)
            {
                TempData["Error"] = ex.Message;
            }
            return RedirectToAction("Categories");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSubCategory(long id)
        {
            try
            {
                await _categoryAppService.DeleteSubCategoryAsync(id);
                TempData["Success"] = "Alt kategori silindi.";
            }
            catch (Abp.UI.UserFriendlyException ex)
            {
                TempData["Error"] = ex.Message;
            }
            return RedirectToAction("Categories");
        }

        // Takım yönetimi (oluşturma/düzenleme/silme) - sadece Admin
        public async Task<IActionResult> Teams()
        {
            if (!IsAdmin()) return AccessDeniedRedirect();
            var teams = await _teamAppService.GetAllAsync();
            var emps = await _employeeAppService.GetAllAsync(new GetEmployeesInput { MaxResultCount = 1000 });
            ViewBag.Employees = emps.Items.OrderBy(e => e.FullName).ToList();
            return View(teams);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveTeam(CreateUpdateTeamDto input)
        {
            if (!IsAdmin()) return AccessDeniedRedirect();
            if (input.Id > 0) await _teamAppService.UpdateAsync(input);
            else await _teamAppService.CreateAsync(input);
            TempData["Success"] = "Takım kaydedildi.";
            return RedirectToAction("Teams");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteTeam(long id)
        {
            if (!IsAdmin()) return AccessDeniedRedirect();
            try
            {
                await _teamAppService.DeleteAsync(id);
                TempData["Success"] = "Takım silindi.";
            }
            catch (Abp.UI.UserFriendlyException ex)
            {
                TempData["Error"] = ex.Message;
            }
            return RedirectToAction("Teams");
        }

        // Çalışanın takımını atama - sadece Admin (TakımLideri kendi takımını değiştiremez)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignEmployeeTeam(long employeeId, long? teamId)
        {
            if (!IsAdmin()) return AccessDeniedRedirect();
            var emp = await _employeeAppService.GetAsync(employeeId);
            var dto = ObjectMapper.Map<Employees.Dto.CreateUpdateEmployeeDto>(emp);
            dto.TeamId = teamId;
            await _employeeAppService.UpdateAsync(dto);
            TempData["Success"] = "Çalışanın takımı güncellendi.";
            return RedirectToAction("Teams");
        }

        // E-posta / SMTP ayarları - sadece Admin
        public async Task<IActionResult> EmailSettings()
        {
            if (!IsAdmin()) return AccessDeniedRedirect();
            var settings = await _emailSettingsAppService.GetAsync();
            return View(settings);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveEmailSettings(UpdateEmailSettingsDto input)
        {
            if (!IsAdmin()) return AccessDeniedRedirect();
            await _emailSettingsAppService.UpdateAsync(input);
            TempData["Success"] = "E-posta ayarları kaydedildi.";
            return RedirectToAction("EmailSettings");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendTestEmail(string testEmail)
        {
            if (!IsAdmin()) return AccessDeniedRedirect();
            try
            {
                await _emailSettingsAppService.SendTestEmailAsync(testEmail);
                TempData["Success"] = $"Test e-postası {testEmail} adresine gönderildi.";
            }
            catch (Abp.UI.UserFriendlyException ex)
            {
                TempData["Error"] = ex.Message;
            }
            return RedirectToAction("EmailSettings");
        }

        // Sistem Logları (Audit) — sadece Admin
        public async Task<IActionResult> SystemLogs(ActivityManagement.Auditing.Dto.GetAuditLogsInput input)
        {
            if (!IsAdmin()) return AccessDeniedRedirect();
            input.MaxResultCount = input.MaxResultCount > 0 ? input.MaxResultCount : 100;
            var result = await _auditLogAppService.GetAllAsync(input);
            ViewBag.Input = input;
            ViewBag.TotalCount = result.TotalCount;
            return View(result.Items);
        }

        public async Task<IActionResult> ExportSystemLogsExcel(ActivityManagement.Auditing.Dto.GetAuditLogsInput input)
        {
            if (!IsAdmin()) return AccessDeniedRedirect();
            input.MaxResultCount = 50000;
            var result = await _auditLogAppService.GetAllAsync(input);

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Sistem Logları");
            var headers = new[] { "Tarih/Saat", "Kullanıcı", "IP", "İşlem", "Varlık", "Kayıt", "Eski Değerler", "Yeni Değerler" };
            for (int i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];
            int row = 2;
            foreach (var a in result.Items)
            {
                ws.Cell(row, 1).Value = a.ExecutionTime.ToString("dd.MM.yyyy HH:mm:ss");
                ws.Cell(row, 2).Value = a.UserName;
                ws.Cell(row, 3).Value = a.ClientIpAddress;
                ws.Cell(row, 4).Value = a.ActionType;
                ws.Cell(row, 5).Value = a.EntityName;
                ws.Cell(row, 6).Value = a.EntityId;
                ws.Cell(row, 7).Value = a.OriginalValues;
                ws.Cell(row, 8).Value = a.NewValues;
                row++;
            }
            ws.Columns().AdjustToContents();
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"SistemLoglari_{DateTime.Today:yyyyMMdd}.xlsx");
        }

        // Alt kategori Sorumluluk Matrisi (Admin + TakımLideri)
        public async Task<IActionResult> Responsibilities()
        {
            ViewBag.Matrix = await _responsibilityAppService.GetAllAsync();
            ViewBag.Categories = await _categoryAppService.GetAllAsync(onlyActive: true);
            ViewBag.Employees = (await _employeeAppService.GetAllListAsync()).Items;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetResponsibility(ActivityManagement.Responsibilities.Dto.SetResponsibilityInput input)
        {
            try
            {
                await _responsibilityAppService.SetAsync(input);
                TempData["Success"] = "Sorumluluk ataması kaydedildi.";
            }
            catch (Abp.UI.UserFriendlyException ex) { TempData["Uyari"] = ex.Message; }
            return RedirectToAction("Responsibilities");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveResponsibility(long id)
        {
            try
            {
                await _responsibilityAppService.RemoveAsync(id);
                TempData["Success"] = "Sorumluluk kaldırıldı.";
            }
            catch (Abp.UI.UserFriendlyException ex) { TempData["Uyari"] = ex.Message; }
            return RedirectToAction("Responsibilities");
        }
    }
}
