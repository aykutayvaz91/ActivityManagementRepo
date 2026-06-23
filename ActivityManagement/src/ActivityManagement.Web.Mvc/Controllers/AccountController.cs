using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ActivityManagement.Web.Controllers
{
    [AllowAnonymous]
    public class AccountController : ActivityManagementControllerBase
    {
        // Google ile giriş başlat
        public IActionResult Login(string returnUrl = "/")
        {
            var props = new AuthenticationProperties { RedirectUri = returnUrl };
            return Challenge(props, GoogleDefaults.AuthenticationScheme);
        }

        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Redirect("/Account/LoggedOut");
        }

        public IActionResult LoggedOut()
        {
            return View();
        }

        public IActionResult Denied()
        {
            return View();
        }
    }
}
