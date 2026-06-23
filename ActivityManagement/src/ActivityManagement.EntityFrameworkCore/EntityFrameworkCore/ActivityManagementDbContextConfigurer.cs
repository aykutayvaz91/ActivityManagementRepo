using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace ActivityManagement.EntityFrameworkCore
{
    public static class ActivityManagementDbContextConfigurer
    {
        public static void Configure(DbContextOptionsBuilder<ActivityManagementDbContext> builder, string connectionString)
        {
            builder.UseSqlServer(connectionString);
        }

        public static void Configure(DbContextOptionsBuilder<ActivityManagementDbContext> builder, DbConnection connection)
        {
            builder.UseSqlServer(connection);
        }
    }
}
