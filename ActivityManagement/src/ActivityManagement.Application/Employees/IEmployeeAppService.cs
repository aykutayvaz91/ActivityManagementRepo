using System.Threading.Tasks;
using Abp.Application.Services;
using Abp.Application.Services.Dto;
using ActivityManagement.Employees.Dto;

namespace ActivityManagement.Employees
{
    public interface IEmployeeAppService : IApplicationService
    {
        Task<PagedResultDto<EmployeeDto>> GetAllAsync(GetEmployeesInput input);
        Task<EmployeeDto> GetAsync(long id);
        Task<EmployeeDto> GetCardAsync(long id);
        Task<EmployeeDto> CreateAsync(CreateUpdateEmployeeDto input);
        Task<EmployeeDto> UpdateAsync(CreateUpdateEmployeeDto input);
        Task DeleteAsync(long id);
        Task<ListResultDto<EmployeeDto>> GetAllListAsync();
    }
}
