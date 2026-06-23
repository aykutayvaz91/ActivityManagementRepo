using Abp.EntityFrameworkCore;
using Abp.EntityFrameworkCore.Configuration;
using Abp.Modules;
using Abp.Reflection.Extensions;
using Abp.Zero.EntityFrameworkCore;

namespace ActivityManagement.EntityFrameworkCore
{
    [DependsOn(
        typeof(ActivityManagementCoreModule),
        typeof(AbpZeroCoreEntityFrameworkCoreModule),
        typeof(AbpEntityFrameworkCoreModule))]
    public class ActivityManagementEntityFrameworkCoreModule : AbpModule
    {
        public bool SkipDbContextRegistration { get; set; }

        public override void PreInitialize()
        {
            if (!SkipDbContextRegistration)
            {
                Configuration.Modules.AbpEfCore().AddDbContext<ActivityManagementDbContext>(options =>
                {
                    if (options.ExistingConnection != null)
                        ActivityManagementDbContextConfigurer.Configure(options.DbContextOptions, options.ExistingConnection);
                    else
                        ActivityManagementDbContextConfigurer.Configure(options.DbContextOptions, options.ConnectionString);
                });
            }
        }

        public override void Initialize()
        {
            IocManager.RegisterAssemblyByConvention(typeof(ActivityManagementEntityFrameworkCoreModule).GetAssembly());
        }
    }
}
