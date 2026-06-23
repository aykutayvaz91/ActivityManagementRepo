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
            var webDir = Path.Combine(Directory.GetCurrentDirectory(), "../ActivityManagement.Web.Mvc");
            var configuration = new ConfigurationBuilder()
                .SetBasePath(webDir)
                .AddJsonFile("appsettings.json", optional: false)
                .AddJsonFile("appsettings.Production.json", optional: true)  // gerçek connection string burada
                .AddEnvironmentVariables()
                .Build();

            var builder = new DbContextOptionsBuilder<ActivityManagementDbContext>();
            var connectionString = configuration.GetConnectionString("Default");

            builder.UseSqlServer(connectionString);

            return new ActivityManagementDbContext(builder.Options);
        }
    }
}
