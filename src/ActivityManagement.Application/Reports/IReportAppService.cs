using System.Threading.Tasks;
using Abp.Application.Services;
using ActivityManagement.Reports.Dto;

namespace ActivityManagement.Reports
{
    public interface IReportAppService : IApplicationService
    {
        Task<PersonalReportDto> GetPersonalReportAsync(GetReportInput input);
        Task<TeamReportDto> GetTeamReportAsync(GetReportInput input);
    }
}
