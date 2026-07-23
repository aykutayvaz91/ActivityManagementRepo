using Abp.Application.Services.Dto;
using Abp.AutoMapper;
using ActivityManagement.Entities;

namespace ActivityManagement.Activities.Dto
{
    [AutoMapFrom(typeof(ActivitySubject))]
    public class ActivitySubjectDto : FullAuditedEntityDto<long>
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public long? CategoryId { get; set; }
        public string CategoryName { get; set; }
        public long? SubCategoryId { get; set; }
        public string SubCategoryName { get; set; }
        public long? ProjectId { get; set; }
        public string ProjectName { get; set; }
        public long? CreatedByLeaderId { get; set; }
        public string CreatedByLeaderName { get; set; }
        public long? AssignedEmployeeId { get; set; }
        public string AssignedEmployeeName { get; set; }
        public long? TeamId { get; set; }
        public string TeamName { get; set; }
        public bool IsActive { get; set; }

        // Özet
        public int LogCount { get; set; }
        public decimal TotalHours { get; set; }

        // Sunucu tarafı yetki: yönetici veya konuyu tanımlayan lider düzenleyebilir/silebilir
        public bool CanManage { get; set; }
        // Giriş yapan bu konuya efor girebilir mi (atanan uzman veya yönetici)
        public bool CanLogEffort { get; set; }
    }

    public class CreateUpdateActivitySubjectDto
    {
        public long Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public long? CategoryId { get; set; }
        public long? SubCategoryId { get; set; }
        public long? ProjectId { get; set; }
        public long? AssignedEmployeeId { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class GetActivitySubjectsInput : PagedAndSortedResultRequestDto
    {
        public long? CategoryId { get; set; }
        public long? SubCategoryId { get; set; }
        public long? AssignedEmployeeId { get; set; }
        public long? ProjectId { get; set; }
        public bool? OnlyActive { get; set; }
    }
}
