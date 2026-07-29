using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ActivityManagement.Search;
using ActivityManagement.Search.Dto;

namespace ActivityManagement.Web.Controllers
{
    // Üst bar global araması. Giriş zorunluluğu global [Authorize] filtresiyle sağlanır.
    public class SearchController : ActivityManagementControllerBase
    {
        private readonly IGlobalSearchAppService _search;
        public SearchController(IGlobalSearchAppService search) { _search = search; }

        public async Task<IActionResult> Index(string q)
        {
            try
            {
                var result = await _search.SearchAsync(q ?? "");
                return View(result);
            }
            catch (Exception ex)
            {
                ActivityManagement.Logging.ErrorLog.Write(ex, "Search/Index");
                TempData["Uyari"] = "Arama sırasında bir sorun oluştu.";
                return View(new GlobalSearchResultDto { Query = q });
            }
        }
    }
}
