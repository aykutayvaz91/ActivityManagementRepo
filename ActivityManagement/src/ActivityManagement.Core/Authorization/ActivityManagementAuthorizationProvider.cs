using Abp.Authorization;
using Abp.Localization;
using Abp.MultiTenancy;

namespace ActivityManagement.Authorization
{
    public class ActivityManagementAuthorizationProvider : AuthorizationProvider
    {
        public override void SetPermissions(IPermissionDefinitionContext context)
        {
            var pages = context.GetPermissionOrNull(ActivityManagementPermissions.GroupName)
                        ?? context.CreatePermission(ActivityManagementPermissions.GroupName,
                               L("ActivityManagement"), multiTenancySides: MultiTenancySides.Host | MultiTenancySides.Tenant);

            // Employees
            var employees = pages.CreateChildPermission(ActivityManagementPermissions.Employees.Default, L("Employees"));
            employees.CreateChildPermission(ActivityManagementPermissions.Employees.Create, L("Create"));
            employees.CreateChildPermission(ActivityManagementPermissions.Employees.Edit,   L("Edit"));
            employees.CreateChildPermission(ActivityManagementPermissions.Employees.Delete, L("Delete"));

            // Projects
            var projects = pages.CreateChildPermission(ActivityManagementPermissions.Projects.Default, L("Projects"));
            projects.CreateChildPermission(ActivityManagementPermissions.Projects.Create, L("Create"));
            projects.CreateChildPermission(ActivityManagementPermissions.Projects.Edit,   L("Edit"));
            projects.CreateChildPermission(ActivityManagementPermissions.Projects.Delete, L("Delete"));

            // Tasks
            var tasks = pages.CreateChildPermission(ActivityManagementPermissions.Tasks.Default, L("Tasks"));
            tasks.CreateChildPermission(ActivityManagementPermissions.Tasks.Create, L("Create"));
            tasks.CreateChildPermission(ActivityManagementPermissions.Tasks.Edit,   L("Edit"));
            tasks.CreateChildPermission(ActivityManagementPermissions.Tasks.Delete, L("Delete"));
            tasks.CreateChildPermission(ActivityManagementPermissions.Tasks.Assign, L("AssignTask"));

            // Activities
            var activities = pages.CreateChildPermission(ActivityManagementPermissions.Activities.Default, L("Activities"));
            activities.CreateChildPermission(ActivityManagementPermissions.Activities.Create, L("Create"));
            activities.CreateChildPermission(ActivityManagementPermissions.Activities.Delete, L("Delete"));

            // Reports
            var reports = pages.CreateChildPermission(ActivityManagementPermissions.Reports.Default, L("Reports"));
            reports.CreateChildPermission(ActivityManagementPermissions.Reports.Personal, L("PersonalReport"));
            reports.CreateChildPermission(ActivityManagementPermissions.Reports.Team,     L("TeamReport"));
            reports.CreateChildPermission(ActivityManagementPermissions.Reports.Export,   L("ExportReport"));

            // RoutineTasks
            var routines = pages.CreateChildPermission(ActivityManagementPermissions.RoutineTasks.Default, L("RoutineTasks"));
            routines.CreateChildPermission(ActivityManagementPermissions.RoutineTasks.Create, L("Create"));
            routines.CreateChildPermission(ActivityManagementPermissions.RoutineTasks.Edit,   L("Edit"));
            routines.CreateChildPermission(ActivityManagementPermissions.RoutineTasks.Delete, L("Delete"));
        }

        private static ILocalizableString L(string name)
            => new LocalizableString(name, ActivityManagementConsts.LocalizationSourceName);
    }
}
