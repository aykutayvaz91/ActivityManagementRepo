using ActivityManagement.Entities;

namespace ActivityManagement.Responsibilities.Dto
{
    public class SubCategoryResponsibilityDto
    {
        public long Id { get; set; }
        public long SubCategoryId { get; set; }
        public string SubCategoryName { get; set; }
        public long CategoryId { get; set; }
        public string CategoryName { get; set; }
        public long EmployeeId { get; set; }
        public string EmployeeName { get; set; }
        public ResponsibilityType ResponsibilityType { get; set; }
        public string TypeText { get; set; }
    }

    public class SetResponsibilityInput
    {
        public long SubCategoryId { get; set; }
        public long EmployeeId { get; set; }
        public ResponsibilityType ResponsibilityType { get; set; }
    }
}
