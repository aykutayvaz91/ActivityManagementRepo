using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Abp.Domain.Repositories;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ActivityManagement.Entities;
using ActivityManagement.Security;

namespace ActivityManagement.Web.Controllers
{
    [AllowAnonymous]
    public class AccountController : ActivityManagementControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly IRepository<Employee, long> _employeeRepository;

        public AccountController(IConfiguration configuration, IRepository<Employee, long> employeeRepository)
        {
            _configuration = configuration;
            _employeeRepository = employeeRepository;
        }

        // Config-admin'i bir Employee kaydına bağlar (yoksa "Sistem Yöneticisi" oluşturur).
        // Böylece admin de EmployeeId sahibi olur; efor/görev/kişi kartı gibi kişi-kimliği gerektiren
        // akışlar admin için de çalışır ve admin-oluşturan kayıtlarda Atayan/Oluşturan alanı dolar.
        private async Task<(long? Id, string Name)> EnsureAdminEmployeeIdAsync(string adminEmail)
        {
            try
            {
                using (var uow = UnitOfWorkManager.Begin())
                {
                    var emp = await _employeeRepository.GetAll().IgnoreQueryFilters()
                        .FirstOrDefaultAsync(e => e.Email == adminEmail);
                    if (emp == null)
                    {
                        var newEmp = new Employee
                        {
                            TenantId = 1,
                            FirstName = "Sistem",
                            LastName = "Yöneticisi",
                            Title = "Sistem Yöneticisi",
                            Department = "Yönetim",
                            Email = adminEmail,
                            AppRole = "Admin",
                            IsActive = true,
                            IsSystemAccount = true, // Personeller listesinde/sayımında gösterilmez
                            HireDate = DateTime.Today
                        };
                        var id = await _employeeRepository.InsertAndGetIdAsync(newEmp);
                        await uow.CompleteAsync();
                        return (id, newEmp.FullName);
                    }
                    await uow.CompleteAsync();
                    return (emp.Id, emp.FullName);
                }
            }
            catch { return (null, null); } // employee bağlanamazsa admin yine de giriş yapar (null-safe akışlar devrede)
        }

        // Giriş ekranı (Google + Admin)
        public IActionResult Login(string returnUrl = "/")
        {
            ViewBag.ReturnUrl = returnUrl;
            ViewBag.GoogleEnabled = !string.IsNullOrWhiteSpace(_configuration["Authentication:Google:ClientId"]);
            return View();
        }

        // Google ile çalışan girişi
        public IActionResult GoogleLogin(string returnUrl = "/")
        {
            var props = new AuthenticationProperties { RedirectUri = returnUrl };
            return Challenge(props, GoogleDefaults.AuthenticationScheme);
        }

        // Admin kullanıcı adı / şifre girişi
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdminLogin(string username, string password, string returnUrl = "/Admin")
        {
            var adminEmail = _configuration["Admin:Email"] ?? "admin@cmit.com.tr";
            // GÜVENLİK: Admin:Password yapılandırılmamışsa girişi TAMAMEN REDDET (bilinen varsayılan "Admin123!" fallback'i kaldırıldı).
            var adminPassword = DpapiProtector.Unprotect(_configuration["Admin:Password"]);

            bool userOk = string.Equals(username?.Trim(), adminEmail, StringComparison.OrdinalIgnoreCase);
            // Sabit-zamanlı parola karşılaştırması (timing side-channel önlenir).
            bool passOk = !string.IsNullOrEmpty(adminPassword) && !string.IsNullOrEmpty(password)
                && System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                    System.Text.Encoding.UTF8.GetBytes(password), System.Text.Encoding.UTF8.GetBytes(adminPassword));
            if (userOk && passOk)
            {
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, adminEmail),
                    new Claim(ClaimTypes.Email, adminEmail),
                    new Claim(ClaimTypes.Role, "Admin"),
                    new Claim("IsAdmin", "true")
                };
                // Admin'i bir Employee kaydına bağla → EmployeeId claim'i (kişi-kimliği gerektiren akışlar çalışsın)
                var adminEmp = await EnsureAdminEmployeeIdAsync(adminEmail);
                if (adminEmp.Id.HasValue)
                {
                    claims.Add(new Claim("EmployeeId", adminEmp.Id.Value.ToString()));
                    // Admin'in KENDİ (Sistem Yöneticisi) personel id'si — login-as'te "kendisi mi başkası mı" ayrımı için sabit tutulur
                    claims.Add(new Claim("AdminOwnEmployeeId", adminEmp.Id.Value.ToString()));
                    if (!string.IsNullOrEmpty(adminEmp.Name))
                        claims.Add(new Claim("ActingAsName", adminEmp.Name));
                }

                var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(identity),
                    new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8) });

                return Redirect(string.IsNullOrEmpty(returnUrl) ? "/Admin" : returnUrl);
            }

            TempData["AdminError"] = "Kullanıcı adı veya şifre hatalı.";
            return RedirectToAction("Login");
        }

        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Redirect("/Account/LoggedOut");
        }

        public IActionResult LoggedOut() => View();

        public IActionResult Denied() => View();
    }
}
