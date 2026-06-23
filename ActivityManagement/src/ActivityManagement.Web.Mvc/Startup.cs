using System;
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
            var googleClientSecret = _configuration["Authentication:Google:ClientSecret"];
            var allowedDomain = _configuration["Authentication:Google:HostedDomain"] ?? "cmit.com.tr";
            var googleEnabled = !string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret);

            services.AddControllersWithViews(options =>
            {
                // Google yapılandırıldıysa giriş zorunlu (tüm sayfalar kimlik doğrulaması ister).
                if (googleEnabled)
                {
                    var policy = new AuthorizationPolicyBuilder()
                        .RequireAuthenticatedUser()
                        .Build();
                    options.Filters.Add(new AuthorizeFilter(policy));
                }
            })
            .AddNewtonsoftJson();

            services.AddSession();

            if (googleEnabled)
            {
                services.AddAuthentication(options =>
                {
                    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
                })
                .AddCookie(options =>
                {
                    options.LoginPath = "/Account/Login";
                    options.AccessDeniedPath = "/Account/Denied";
                })
                .AddGoogle(options =>
                {
                    options.ClientId = googleClientId;
                    options.ClientSecret = googleClientSecret;
                    options.CallbackPath = "/signin-google";
                    options.SaveTokens = true;
                    options.Scope.Add("email");
                    options.Scope.Add("profile");

                    // Hesap seçiminde sadece cmit.com.tr alanını öner
                    options.Events.OnRedirectToAuthorizationEndpoint = context =>
                    {
                        var uri = context.RedirectUri;
                        if (!uri.Contains("&hd=")) uri += "&hd=" + allowedDomain;
                        context.Response.Redirect(uri);
                        return Task.CompletedTask;
                    };

                    // Sunucu tarafı sıkı kontrol: e-posta alanı cmit.com.tr değilse reddet
                    options.Events.OnTicketReceived = context =>
                    {
                        var email = context.Principal?.FindFirst(ClaimTypes.Email)?.Value;
                        if (string.IsNullOrEmpty(email) ||
                            !email.EndsWith("@" + allowedDomain, StringComparison.OrdinalIgnoreCase))
                        {
                            context.Response.Redirect("/Account/Denied");
                            context.HandleResponse();
                        }
                        return Task.CompletedTask;
                    };
                });
            }

            return services.AddAbp<ActivityManagementWebMvcModule>();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILoggerFactory loggerFactory)
        {
            app.UseAbp();

            // Gerçek hatayı görebilmek için her ortamda geliştirici hata sayfası
            app.UseDeveloperExceptionPage();

            app.UseStaticFiles();
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseSession();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
            });

            // Veritabanını oluştur ve seed data ekle.
            // DbContext'i ABP UoW'dan değil, elle kuruyoruz (DbContextOptions Windsor'a kayıtlı değil).
            try
            {
                var optionsBuilder = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<ActivityManagementDbContext>();
                optionsBuilder.UseSqlServer(_configuration.GetConnectionString("Default"));

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
