using System.Collections.Generic;
using Abp.Application.Services.Dto;

namespace ActivityManagement.Categories.Dto
{
    public class CategoryDto : FullAuditedEntityDto<long>
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public long? ResponsibleEmployee1Id { get; set; }
        public string ResponsibleEmployee1Name { get; set; }
        public long? ResponsibleEmployee2Id { get; set; }
        public string ResponsibleEmployee2Name { get; set; }
        public bool IsActive { get; set; }
        public long? TeamId { get; set; }
        public string TeamName { get; set; }
        public List<SubCategoryDto> SubCategories { get; set; } = new List<SubCategoryDto>();
    }

    public class CreateUpdateCategoryDto
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public long? ResponsibleEmployee1Id { get; set; }
        public long? ResponsibleEmployee2Id { get; set; }
        public bool IsActive { get; set; } = true;
        public long? TeamId { get; set; }
    }
}
