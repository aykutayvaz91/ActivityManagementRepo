using Abp.Application.Services.Dto;

namespace ActivityManagement.Categories.Dto
{
    public class SubCategoryDto : FullAuditedEntityDto<long>
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public long CategoryId { get; set; }
        public string CategoryName { get; set; }
        public bool IsActive { get; set; }
    }

    public class CreateUpdateSubCategoryDto
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public long CategoryId { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
