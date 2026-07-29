using System.Threading.Tasks;
using Abp.Application.Services;
using Abp.Application.Services.Dto;
using ActivityManagement.Auditing.Dto;

namespace ActivityManagement.Auditing
{
    public interface IAuditLogAppService : IApplicationService
    {
        Task<PagedResultDto<AuditLogDto>> GetAllAsync(GetAuditLogsInput input);
        Task<PagedResultDto<AuditLogDto>> GetArchiveAsync(GetAuditLogsInput input);
        Task<ArchiveSummaryDto> GetArchiveSummaryAsync();
    }
}
