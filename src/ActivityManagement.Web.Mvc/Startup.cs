using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Abp.AspNetCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ActivityManagement.EntityFrameworkCore;
using ActivityManagement.Security;
using ActivityManagement.Web.Bootstrapping;

namespace ActivityManagement.Web
{
    public class Startup
    {
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _configuration;

        public Startup(IWebHostEnvironment env, IConfiguration configuration)
        {
            _env = env;
            _configuration = configuration;
        }

        public IServiceProvider ConfigureServices(IServiceCollection services)
        {
            var googleClientId = _configuration["Authentication:Google:ClientId"];
            var googleClientSecret = DpapiProtector.Unprotect(_configuration["Authentication:Google:ClientSecret"]);
            var allowedDomain = _configuration["Authentication:Google:HostedDomain"] ?? "cmit.com.tr";
            var googleEnabled = !string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret);
            var connStr = DpapiProtector.Unprotect(_configuration.GetConnectionString("Default"));

            // Tüm sayfalar giriş ister; giriş ekranı (/Account/Login) anonim.
            services.AddControllersWithViews(options =>
            {
                var policy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
                options.Filters.Add(new AuthorizeFilter(policy));
                // Ondalık alanlar kültürden bağımsız (invariant) bağlansın (tr-TR virgül sorununu önler)
                options.ModelBinderProviders.Insert(0, new ActivityManagement.Web.Utils.InvariantDecimalModelBinderProvider());
            })
            .AddNewtonsoftJson();

            // Tarih/saat gösterimini dd.MM.yyyy standardına çekmek için tüm istekleri tr-TR kültürüne sabitle.
            var trCulture = new System.Globalization.CultureInfo("tr-TR");
            var supportedCultures = new[] { trCulture };
            services.Configure<Microsoft.AspNetCore.Builder.RequestLocalizationOptions>(options =>
            {
                options.DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture(trCulture);
                options.SupportedCultures = supportedCultures;
                options.SupportedUICultures = supportedCultures;
            });

            services.AddSession();
            services.AddHttpContextAccessor();

            // Günlük SLA hatırlatma servisi (SMTP yapılandırılmamışsa no-op)
            services.AddHostedService<ActivityManagement.Web.BackgroundJobs.SlaReminderHostedService>();

            // Otomatik arşiv: tamamlandığı AY geçen görevleri "Kapatıldı"ya çeker (~12 saatte bir)
            services.AddHostedService<ActivityManagement.Web.BackgroundJobs.TaskAutoCloseHostedService>();

            // FAZ 2 — Talep PULL senkron servisi (Admin → Entegrasyon; varsayılan kapalı, no-op)
            services.AddHttpClient();
            services.AddHostedService<ActivityManagement.Web.BackgroundJobs.RequestSyncHostedService>();

            var authBuilder = services.AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            })
            .AddCookie(options =>
            {
                options.LoginPath = "/Account/Login";
                options.AccessDeniedPath = "/Account/Denied";
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
            });

            if (googleEnabled)
            {
                authBuilder.AddGoogle(options =>
                {
                    options.ClientId = googleClientId;
                    options.ClientSecret = googleClientSecret;
                    options.CallbackPath = "/signin-google";
                    options.SaveTokens = true;
                    options.Scope.Add("email");
                    options.Scope.Add("profile");

                    options.Events.OnRedirectToAuthorizationEndpoint = context =>
                    {
                        var uri = context.RedirectUri;
                        if (!uri.Contains("&hd=")) uri += "&hd=" + allowedDomain;
                        context.Response.Redirect(uri);
                        return Task.CompletedTask;
                    };

                    options.Events.OnTicketReceived = context =>
                    {
                        var email = context.Principal?.FindFirst(ClaimTypes.Email)?.Value;
                        if (string.IsNullOrEmpty(email) ||
                            !email.EndsWith("@" + allowedDomain, StringComparison.OrdinalIgnoreCase))
                        {
                            context.Response.Redirect("/Account/Denied");
                            context.HandleResponse();
                            return Task.CompletedTask;
                        }

                        // Çalışanın rol + kimliğini DB'den oku, claim ekle
                        var (role, empId) = LookupEmployee(connStr, email);
                        if (context.Principal?.Identity is ClaimsIdentity id)
                        {
                            id.AddClaim(new Claim(ClaimTypes.Role, role));
                            if (empId.HasValue)
                                id.AddClaim(new Claim("EmployeeId", empId.Value.ToString()));
                            if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
                                id.AddClaim(new Claim("IsAdmin", "true"));
                        }
                        return Task.CompletedTask;
                    };
                });
            }

            return services.AddAbp<ActivityManagementWebMvcModule>();
        }

        // Google girişinde e-postaya göre çalışan rol + kimliğini DB'den çeker (login anında tek sorgu).
        private static (string Role, long? EmployeeId) LookupEmployee(string connectionString, string email)
        {
            try
            {
                var ob = new DbContextOptionsBuilder<ActivityManagementDbContext>();
                ob.UseSqlServer(connectionString);
                using var db = new ActivityManagementDbContext(ob.Options);
                var emp = db.Employees.IgnoreQueryFilters()
                    .FirstOrDefault(e => e.Email == email);
                if (emp == null) return ("Uzman", null);
                return (string.IsNullOrWhiteSpace(emp.AppRole) ? "Uzman" : emp.AppRole, emp.Id);
            }
            catch { return ("Uzman", null); }
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILoggerFactory loggerFactory)
        {
            // Dosya logları: audit + hata (gün gün, büyürse -1/-2 parçalı)
            ActivityManagement.Auditing.AuditFileLogger.Configure(
                System.IO.Path.Combine(env.ContentRootPath, "logs", "audit"));
            ActivityManagement.Logging.ErrorLog.Configure(
                System.IO.Path.Combine(env.ContentRootPath, "logs", "error"));

            app.UseAbp();

            // Hatalarda sayfa patlamasın: kullanıcı dostu hata sayfası + dosyaya loglama (/Home/Error).
            app.UseExceptionHandler("/Home/Error");

            app.UseStaticFiles();

            // Tarih/sayı formatı için kültürü tr-TR'ye sabitle (dd.MM.yyyy)
            app.UseRequestLocalization();

            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseSession();

            // Denetim (audit) için giriş yapan kullanıcıyı ambient bağlama taşı (DbContext SaveChanges okur)
            app.Use(async (context, next) =>
            {
                var u = context.User;
                long? empId = long.TryParse(u?.FindFirst("EmployeeId")?.Value, out var e) ? e : (long?)null;
                var name = u?.FindFirst(ClaimTypes.Name)?.Value ?? u?.FindFirst(ClaimTypes.Email)?.Value;
                ActivityManagement.Auditing.AuditUserContext.Current = new ActivityManagement.Auditing.AuditUser
                {
                    UserId = empId,
                    UserName = name,
                    Ip = context.Connection?.RemoteIpAddress?.ToString()
                };
                await next();
            });

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
            });

            // Veritabanını oluştur ve seed data ekle.
            // DbContext'i ABP UoW'dan değil, elle kuruyoruz (DbContextOptions Windsor'a kayıtlı değil).
            try
            {
                var optionsBuilder = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<ActivityManagementDbContext>();
                optionsBuilder.UseSqlServer(DpapiProtector.Unprotect(_configuration.GetConnectionString("Default")));

                using (var dbContext = new ActivityManagementDbContext(optionsBuilder.Options))
                {
                    dbContext.Database.Migrate();
                    new EntityFrameworkCore.Seed.SeedDataBuilder(dbContext).Create();
                }
            }
            catch (Exception ex)
            {
                // Seed başarısız olursa uygulamayı düşürme, sadece logla.
                loggerFactory.CreateLogger<Startup>().LogError(ex, "Seed data oluşturulurken hata oluştu.");
            }
        }
    }
}
