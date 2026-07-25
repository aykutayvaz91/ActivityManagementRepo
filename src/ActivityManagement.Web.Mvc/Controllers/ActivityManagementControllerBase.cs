using System.Threading.Tasks;
using Abp.AspNetCore.Mvc.Controllers;
using Abp.AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ActivityManagement.Web.Controllers
{
    public abstract class ActivityManagementControllerBase : AbpController
    {
        protected ActivityManagementControllerBase()
        {
            LocalizationSourceName = ActivityManagementConsts.LocalizationSourceName;
        }

        // V4: Tema ayarlarını action penceresinde (ABP UoW açıkken) yükleyip ViewData'ya koy.
        // (Layout'tan AppService çağrısı UoW kapandığı için çalışmıyordu; burada güvenle çalışır.)
        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            try
            {
                using (var uow = UnitOfWorkManager.Begin())
                {
                    // Tema
                    var themeSvc = HttpContext.RequestServices.GetService(typeof(ActivityManagement.Theming.IThemeSettingsAppService))
                              as ActivityManagement.Theming.IThemeSettingsAppService;
                    if (themeSvc != null)
                    {
                        var t = await themeSvc.GetAsync();
                        ViewData["ThemePrimary"] = t.PrimaryColor;
                        ViewData["ThemeLogo"] = t.LogoUrl;
                        // Efektif marka: "takıma göre" açıksa kişinin takım kısa adı (INFRA...), yoksa marka adı
                        ViewData["ThemeBrand"] = string.IsNullOrWhiteSpace(t.EffectiveBrand) ? t.BrandName : t.EffectiveBrand;
                    }

                    // Rol × Sayfa erişimi — izinli sayfa anahtarları (menü gizleme + action guard)
                    var acl = HttpContext.RequestServices.GetService(typeof(ActivityManagement.Authorization.IAccessControlAppService))
                              as ActivityManagement.Authorization.IAccessControlAppService;
                    if (acl != null)
                    {
                        var role = User?.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "Uzman";
                        var allowed = await acl.GetAllowedPagesAsync(role);
                        _allowedPages = new System.Collections.Generic.HashSet<string>(allowed, System.StringComparer.OrdinalIgnoreCase);
                        ViewData["AllowedPages"] = _allowedPages;
                    }
                    await uow.CompleteAsync();
                }
            }
            catch { /* okunamazsa: tema varsayılan, erişim fail-open (helper null => izinli) */ }
            await next();
        }

        // Rol × Sayfa erişim seti (OnActionExecutionAsync'te doldurulur). Null => henüz yüklenmedi (fail-open).
        private System.Collections.Generic.HashSet<string> _allowedPages;

        // Sayfa erişim kontrolü. Yüklenememişse (null) veya Admin ise izinli sayılır (kilitlenme önlemi).
        protected bool CanAccessPage(string pageKey)
        {
            if (User != null && User.IsInRole("Admin")) return true;
            if (_allowedPages == null) return true; // fail-open
            return _allowedPages.Contains(pageKey);
        }

        // Erişim yoksa toast + güvenli yönlendirme (AJAX'ta 403).
        protected IActionResult EnsurePageAccess(string pageKey)
        {
            if (CanAccessPage(pageKey)) return null;
            return AccessDeniedRedirect();
        }

        // Yetkisiz erişim: /Account/Denied sayfasına atmak yerine bilgi mesajı (sağdan toast) bırakıp
        // güvenli bir sayfaya döner. AJAX isteklerinde 403 döner (istemci toast gösterir).
        // Not: Session ölünce (kimlik doğrulanmamış) cookie middleware zaten Login'e yönlendirir.
        protected IActionResult AccessDeniedRedirect(string fallbackUrl = "/")
        {
            const string msg = "Bu işlem için yetkiniz yok.";
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return StatusCode(403, new { error = msg });
            TempData["Uyari"] = msg;
            return Redirect(fallbackUrl);
        }
    }
}
