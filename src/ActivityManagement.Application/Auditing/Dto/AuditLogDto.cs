using System;
using Abp.Application.Services.Dto;
using Abp.AutoMapper;
using ActivityManagement.Entities;

namespace ActivityManagement.Auditing.Dto
{
    [AutoMapFrom(typeof(SystemAuditLog))]
    public class AuditLogDto : EntityDto<long>
    {
        public long? UserId { get; set; }
        public string UserName { get; set; }
        public DateTime ExecutionTime { get; set; }
        public string ClientIpAddress { get; set; }
        public string ActionType { get; set; }
        public string EntityName { get; set; }
        public string EntityId { get; set; }
        public string OriginalValues { get; set; }
        public string NewValues { get; set; }
    }

    public class GetAuditLogsInput : PagedAndSortedResultRequestDto
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string UserName { get; set; }
        public string ActionType { get; set; }
        public string EntityName { get; set; }
    }
}
