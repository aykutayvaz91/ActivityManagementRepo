using System.Threading.Tasks;
using Abp.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Mvc;
using ActivityManagement.Authorization;
using ActivityManagement.Employees;
using ActivityManagement.Employees.Dto;
using ActivityManagement.Teams;
using ActivityManagement.Responsibilities;

namespace ActivityManagement.Web.Controllers
{
    public class EmployeesController : ActivityManagementControllerBase
    {
        private readonly IEmployeeAppService _employeeAppService;
        private readonly ITeamAppService _teamAppService;
        private readonly ISubCategoryResponsibilityAppService _responsibilityAppService;
        private readonly Microsoft.AspNetCore.Hosting.IWebHostEnvironment _env;
        private readonly ActivityManagement.Web.Helpers.UploadStorage _uploads;

        public EmployeesController(IEmployeeAppService employeeAppService, ITeamAppService teamAppService, ISubCategoryResponsibilityAppService responsibilityAppService, Microsoft.AspNetCore.Hosting.IWebHostEnvironment env, ActivityManagement.Web.Helpers.UploadStorage uploads)
        {
            _employeeAppService = employeeAppService;
            _teamAppService = teamAppService;
            _uploads = uploads;
            _responsibilityAppService = responsibilityAppService;
            _env = env;
        }

        // Profil fotoğrafını depolama kökü altında uploads/photos'a kaydeder, göreli URL döner (V4)
        private async Task<string> SavePhotoAsync(Microsoft.AspNetCore.Http.IFormFile photo)
        {
            if (photo == null || photo.Length == 0) return null;
            // GÜVENLİK: yalnız güvenli görsel türü (svg/html vb. reddedilir → depolanmış XSS önlenir)
            if (!ActivityManagement.Web.Helpers.UploadValidator.IsInlineSafe(photo.FileName))
                throw new Abp.UI.UserFriendlyException("Fotoğraf yalnızca PNG/JPG/GIF/WEBP olabilir.");
            var dir = _uploads.EnsureSubDir("photos");
            var ext = System.IO.Path.GetExtension(photo.FileName);
            var name = System.Guid.NewGuid().ToString("N") + ext;
            var full = System.IO.Path.Combine(dir, name);
            using (var fs = new System.IO.FileStream(full, System.IO.FileMode.Create))
                await photo.CopyToAsync(fs);
            return "/uploads/photos/" + name;
        }

        private bool IsManager() => User.IsInRole("Admin") || User.IsInRole("TakımLideri");

        private long? CurrentEmployeeId()
        {
            var c = User.FindFirst("EmployeeId")?.Value;
            return long.TryParse(c, out var id) ? id : (long?)null;
        }

        // Görüntüleme: rol×sayfa erişimine tabi
        public IActionResult Index()
        {
            var g = EnsurePageAccess("Employees"); if (g != null) return g;
            return View();
        }

        public async Task<IActionResult> Card(long id)
        {
            var employee = await _employeeAppService.GetCardAsync(id);
            try
            {
                ViewBag.Responsibilities = await _responsibilityAppService.GetByEmployeeAsync(id);
            }
            catch
            {
                ViewBag.Responsibilities = new System.Collections.Generic.List<ActivityManagement.Responsibilities.Dto.SubCategoryResponsibilityDto>();
            }
            return View(employee);
        }

        // Kişisel takvim (ayrı sayfa): görevler (son tarih) + faaliyetler
        public async Task<IActionResult> Calendar(long id)
        {
            var employee = await _employeeAppService.GetAsync(id);
            return View(employee);
        }

        // Düzenleme (ekle/güncelle/sil): sadece Admin ve Takım Lideri
        public async Task<IActionResult> Create()
        {
            if (!IsManager()) return AccessDeniedRedirect();
            ViewBag.Teams = await _teamAppService.GetAllAsync(true);
            return View(new CreateUpdateEmployeeDto());
        }

        [HttpPost]
        public async Task<IActionResult> Create(CreateUpdateEmployeeDto input, Microsoft.AspNetCore.Http.IFormFile photoFile)
        {
            if (!IsManager()) return AccessDeniedRedirect();
            if (!ModelState.IsValid)
            {
                ViewBag.Teams = await _teamAppService.GetAllAsync(true);
                return View(input);
            }
            var url = await SavePhotoAsync(photoFile);
            if (url != null) input.PhotoUrl = url;
            await _employeeAppService.CreateAsync(input);
            return RedirectToAction("Index");
        }

        // Düzenleme: Admin/Takım Lideri herkesi, diğerleri sadece kendi kaydını düzenleyebilir
        public async Task<IActionResult> Edit(long id)
        {
            if (!IsManager() && id != CurrentEmployeeId()) return AccessDeniedRedirect();
            var emp = await _employeeAppService.GetAsync(id);
            if (IsManager()) ViewBag.Teams = await _teamAppService.GetAllAsync(true);
            return View(ObjectMapper.Map<CreateUpdateEmployeeDto>(emp));
        }

        [HttpPost]
        public async Task<IActionResult> Edit(CreateUpdateEmployeeDto input, Microsoft.AspNetCore.Http.IFormFile photoFile)
        {
            if (!IsManager() && input.Id != CurrentEmployeeId()) return AccessDeniedRedirect();
            if (!ModelState.IsValid)
            {
                if (IsManager()) ViewBag.Teams = await _teamAppService.GetAllAsync(true);
                return View(input);
            }
            var url = await SavePhotoAsync(photoFile);
            if (url != null) input.PhotoUrl = url;
            var updated = await _employeeAppService.UpdateAsync(input);
            if (!string.IsNullOrWhiteSpace(updated?.HandoverInfo))
                TempData["Uyari"] = "Açık işler yedeğe devredildi → " + updated.HandoverInfo;
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> Delete(long id)
        {
            if (!IsManager()) return AccessDeniedRedirect();
            await _employeeAppService.DeleteAsync(id);
            return RedirectToAction("Index");
        }
    }
}
