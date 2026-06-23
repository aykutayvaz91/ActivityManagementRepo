using System.Threading.Tasks;
using Abp.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Mvc;
using ActivityManagement.Authorization;
using ActivityManagement.Employees;
using ActivityManagement.Employees.Dto;

namespace ActivityManagement.Web.Controllers
{
    public class EmployeesController : ActivityManagementControllerBase
    {
        private readonly IEmployeeAppService _employeeAppService;

        public EmployeesController(IEmployeeAppService employeeAppService)
        {
            _employeeAppService = employeeAppService;
        }

        public IActionResult Index()
        {
            return View();
        }

        public async Task<IActionResult> Card(long id)
        {
            var employee = await _employeeAppService.GetCardAsync(id);
            return View(employee);
        }

        public IActionResult Create()
        {
            return View(new CreateUpdateEmployeeDto());
        }

        [HttpPost]
        public async Task<IActionResult> Create(CreateUpdateEmployeeDto input)
        {
            if (!ModelState.IsValid) return View(input);
            await _employeeAppService.CreateAsync(input);
            return RedirectToAction("Index");
        }

        public async Task<IActionResult> Edit(long id)
        {
            var emp = await _employeeAppService.GetAsync(id);
            return View(ObjectMapper.Map<CreateUpdateEmployeeDto>(emp));
        }

        [HttpPost]
        public async Task<IActionResult> Edit(CreateUpdateEmployeeDto input)
        {
            if (!ModelState.IsValid) return View(input);
            await _employeeAppService.UpdateAsync(input);
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> Delete(long id)
        {
            await _employeeAppService.DeleteAsync(id);
            return RedirectToAction("Index");
        }
    }
}
