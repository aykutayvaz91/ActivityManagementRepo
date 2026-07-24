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
        private async Task<long?> EnsureAdminEmployeeIdAsync(string adminEmail)
        {
            try
            {
                using (var uow = UnitOfWorkManager.Begin())
                {
                    var emp = await _employeeRepository.GetAll().IgnoreQueryFilters()
                        .FirstOrDefaultAsync(e => e.Email == adminEmail);
                    if (emp == null)
                    {
                        var id = await _employeeRepository.InsertAndGetIdAsync(new Employee
                        {
                            TenantId = 1,
                            FirstName = "Sistem",
                            LastName = "Yöneticisi",
                            Title = "Sistem Yöneticisi",
                            Department = "Yönetim",
                            Email = adminEmail,
                            AppRole = "Admin",
                            IsActive = true,
                            HireDate = DateTime.Today
                        });
                        await uow.CompleteAsync();
                        return id;
                    }
                    await uow.CompleteAsync();
                    return emp.Id;
                }
            }
            catch { return null; } // employee bağlanamazsa admin yine de giriş yapar (null-safe akışlar devrede)
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
            var adminPassword = DpapiProtector.Unprotect(_configuration["Admin:Password"]) ?? "Admin123!";

            if (string.Equals(username?.Trim(), adminEmail, StringComparison.OrdinalIgnoreCase) &&
                password == adminPassword)
            {
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, adminEmail),
                    new Claim(ClaimTypes.Email, adminEmail),
                    new Claim(ClaimTypes.Role, "Admin"),
                    new Claim("IsAdmin", "true")
                };
                // Admin'i bir Employee kaydına bağla → EmployeeId claim'i (kişi-kimliği gerektiren akışlar çalışsın)
                var adminEmpId = await EnsureAdminEmployeeIdAsync(adminEmail);
                if (adminEmpId.HasValue)
                    claims.Add(new Claim("EmployeeId", adminEmpId.Value.ToString()));

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
