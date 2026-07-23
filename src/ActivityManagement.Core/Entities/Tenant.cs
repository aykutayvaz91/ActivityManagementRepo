using Abp.MultiTenancy;

namespace ActivityManagement.Entities
{
    public class Tenant : AbpTenant<User>
    {
        public Tenant() { }

        public Tenant(string tenancyName, string name) : base(tenancyName, name)
        {
        }
    }
}
