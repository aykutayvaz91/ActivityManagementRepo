using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using System.IO;

namespace ActivityManagement.EntityFrameworkCore
{
    // EF Core CLI (dotnet ef migrations add) bu fabrikayı kullanarak
    // uygulama çalıştırmadan DbContext oluşturur.
    public class ActivityManagementDbContextFactory : IDesignTimeDbContextFactory<ActivityManagementDbContext>
    {
        public ActivityManagementDbContext CreateDbContext(string[] args)
        {
            // appsettings.json Web.Mvc projesinden okunur
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "../ActivityManagement.Web.Mvc"))
                .AddJsonFile("appsettings.json", optional: false)
                .Build();

            var builder = new DbContextOptionsBuilder<ActivityManagementDbContext>();
            var connectionString = configuration.GetConnectionString("Default");

            builder.UseSqlServer(connectionString);

            return new ActivityManagementDbContext(builder.Options);
        }
    }
}
