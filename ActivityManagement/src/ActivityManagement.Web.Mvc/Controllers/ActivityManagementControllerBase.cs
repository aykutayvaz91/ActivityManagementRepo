using Abp.AspNetCore.Mvc.Controllers;
using Abp.AutoMapper;

namespace ActivityManagement.Web.Controllers
{
    public abstract class ActivityManagementControllerBase : AbpController
    {
        protected ActivityManagementControllerBase()
        {
            LocalizationSourceName = ActivityManagementConsts.LocalizationSourceName;
        }
    }
}
