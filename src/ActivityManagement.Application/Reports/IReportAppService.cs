using System.Threading.Tasks;
using Abp.Application.Services;
using ActivityManagement.Reports.Dto;

namespace ActivityManagement.Reports
{
    public interface IReportAppService : IApplicationService
    {
        // GÜVENLİK (IDOR): rapor kapsam denetimi ReportsController'da yapılır. Bu metotlar iç yetki kontrolü
        // içermediğinden dynamic API'ye AÇILMAZ — aksi halde bir Uzman /api/services/app/report/... ile
        // input.EmployeeId/TeamId vererek başkasının verisini çekebilirdi. Yalnız controller (in-process) çağırır.
        [Abp.Application.Services.RemoteService(false)]
        Task<PersonalReportDto> GetPersonalReportAsync(GetReportInput input);
        [Abp.Application.Services.RemoteService(false)]
        Task<TeamReportDto> GetTeamReportAsync(GetReportInput input);
    }
}
