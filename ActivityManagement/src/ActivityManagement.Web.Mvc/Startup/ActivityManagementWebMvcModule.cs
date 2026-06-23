using System.Reflection;
using Abp.AspNetCore;
using Abp.AspNetCore.Configuration;
using Abp.Modules;
using Abp.Reflection.Extensions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using ActivityManagement.EntityFrameworkCore;

namespace ActivityManagement.Web.Bootstrapping
{
    [DependsOn(
        typeof(ActivityManagementApplicationModule),
        typeof(ActivityManagementEntityFrameworkCoreModule),
        typeof(AbpAspNetCoreModule)
    )]
    public class ActivityManagementWebMvcModule : AbpModule
    {
        private readonly IConfigurationRoot _appConfiguration;

        public ActivityManagementWebMvcModule(IWebHostEnvironment env)
        {
            _appConfiguration = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();
        }

        public override void PreInitialize()
        {
            // Gerçek connection string'i ata (literal "Default" değil)
            Configuration.DefaultNameOrConnectionString = _appConfiguration.GetConnectionString("Default");

            Configuration.Modules.AbpAspNetCore()
                .CreateControllersForAppServices(
                    typeof(ActivityManagementApplicationModule).GetAssembly()
                );
        }

        public override void Initialize()
        {
            IocManager.RegisterAssemblyByConvention(Assembly.GetExecutingAssembly());
        }
    }
}
