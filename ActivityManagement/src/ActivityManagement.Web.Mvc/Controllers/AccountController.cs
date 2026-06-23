using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace ActivityManagement.Web.Controllers
{
    [AllowAnonymous]
    public class AccountController : ActivityManagementControllerBase
    {
        private readonly IConfiguration _configuration;

        public AccountController(IConfiguration configuration)
        {
            _configuration = configuration;
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
            var adminPassword = _configuration["Admin:Password"] ?? "Admin123!";

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
