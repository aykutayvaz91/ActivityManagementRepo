using Abp.Application.Services;

namespace ActivityManagement
{
    public abstract class ActivityManagementAppServiceBase : ApplicationService
    {
        protected ActivityManagementAppServiceBase()
        {
            LocalizationSourceName = ActivityManagementConsts.LocalizationSourceName;
        }
    }
}
