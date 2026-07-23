using System.Reflection;
using Abp.AutoMapper;
using Abp.Modules;

namespace ActivityManagement
{
    [DependsOn(
        typeof(ActivityManagementCoreModule),
        typeof(AbpAutoMapperModule))]
    public class ActivityManagementApplicationModule : AbpModule
    {
        public override void PreInitialize()
        {
            Configuration.Modules.AbpAutoMapper().Configurators.Add(cfg =>
            {
                // Custom mappings go here if needed
            });
        }

        public override void Initialize()
        {
            IocManager.RegisterAssemblyByConvention(Assembly.GetExecutingAssembly());
        }
    }
}
