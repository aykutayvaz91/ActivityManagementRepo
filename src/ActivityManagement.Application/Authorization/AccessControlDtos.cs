using System.Collections.Generic;

namespace ActivityManagement.Authorization
{
    public class AppRoleDefDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public bool IsSystem { get; set; }
        public int SortOrder { get; set; }
    }

    public class PageDefDto
    {
        public string Key { get; set; }
        public string Title { get; set; }
    }

    public class AccessMatrixDto
    {
        public List<PageDefDto> Pages { get; set; } = new List<PageDefDto>();
        public List<AppRoleDefDto> Roles { get; set; } = new List<AppRoleDefDto>();
        // role -> allowed page keys
        public Dictionary<string, List<string>> Allowed { get; set; } = new Dictionary<string, List<string>>();
    }
}
