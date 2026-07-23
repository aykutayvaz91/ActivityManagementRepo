using Abp.Authorization.Roles;

namespace ActivityManagement.Entities
{
    public class Role : AbpRole<User>
    {
        public const string DefaultTenantAdminRoleName = "Admin";

        public Role() { }

        public Role(int? tenantId, string displayName) : base(tenantId, displayName)
        {
        }

        public Role(int? tenantId, string name, string displayName) : base(tenantId, name, displayName)
        {
        }
    }
}
