namespace ActivityManagement.Authorization
{
    public static class ActivityManagementPermissions
    {
        public const string GroupName = "ActivityManagement";

        // Employee
        public static class Employees
        {
            public const string Default = GroupName + ".Employees";
            public const string Create = Default + ".Create";
            public const string Edit   = Default + ".Edit";
            public const string Delete = Default + ".Delete";
        }

        // Project
        public static class Projects
        {
            public const string Default = GroupName + ".Projects";
            public const string Create = Default + ".Create";
            public const string Edit   = Default + ".Edit";
            public const string Delete = Default + ".Delete";
        }

        // Task
        public static class Tasks
        {
            public const string Default = GroupName + ".Tasks";
            public const string Create = Default + ".Create";
            public const string Edit   = Default + ".Edit";
            public const string Delete = Default + ".Delete";
            public const string Assign = Default + ".Assign";
        }

        // Activity
        public static class Activities
        {
            public const string Default = GroupName + ".Activities";
            public const string Create = Default + ".Create";
            public const string Delete = Default + ".Delete";
        }

        // Reports
        public static class Reports
        {
            public const string Default  = GroupName + ".Reports";
            public const string Personal = Default + ".Personal";
            public const string Team     = Default + ".Team";
            public const string Export   = Default + ".Export";
        }

        // RoutineTask
        public static class RoutineTasks
        {
            public const string Default = GroupName + ".RoutineTasks";
            public const string Create = Default + ".Create";
            public const string Edit   = Default + ".Edit";
            public const string Delete = Default + ".Delete";
        }
    }
}
