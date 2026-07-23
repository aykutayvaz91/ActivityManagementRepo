using System.Reflection;
using Abp.Localization;
using Abp.Localization.Dictionaries;
using Abp.Localization.Dictionaries.Xml;
using Abp.Modules;
using Abp.Zero;
using ActivityManagement.Authorization;

namespace ActivityManagement
{
    [DependsOn(typeof(AbpZeroCoreModule))]
    public class ActivityManagementCoreModule : AbpModule
    {
        public override void PreInitialize()
        {
            // Tek-tenant, login'siz çalışması için: tenant/yetki/denetim altyapısını kapat.
            // (Tam ASP.NET Zero host/tenant + admin user seed'i kurulmadığı için.)
            Configuration.MultiTenancy.IsEnabled = false;
            Configuration.Authorization.IsEnabled = false;
            Configuration.Auditing.IsEnabled = false;

            Configuration.Authorization.Providers.Add<ActivityManagementAuthorizationProvider>();

            Configuration.Localization.Sources.Add(
                new DictionaryBasedLocalizationSource(
                    ActivityManagementConsts.LocalizationSourceName,
                    new XmlEmbeddedFileLocalizationDictionaryProvider(
                        Assembly.GetExecutingAssembly(),
                        "ActivityManagement.Localization.ActivityManagement"
                    )
                )
            );
        }

        public override void Initialize()
        {
            IocManager.RegisterAssemblyByConvention(Assembly.GetExecutingAssembly());
        }
    }
}
