using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace ActivityManagement.Web.Helpers
{
    /// <summary>
    /// Yüklenen dosyaların (personel fotoğrafı, görev yorumu eki, marka logosu) FİZİKSEL depolama kökü.
    /// Yapılandırma: <c>Storage:UploadsPath</c> (canlıda <c>D:\Uploads</c> — ayrı storage alanı).
    /// Yol boş/erişilemez ise <c>wwwroot/uploads</c>'a düşer (dev güvenliği; site kilitlenmez).
    /// URL yolu HER DURUMDA <c>/uploads/...</c> olarak korunur → DB'deki mevcut göreli URL'ler bozulmaz.
    /// Static sunum Startup'ta bu köke bağlanır (nosniff + görsel olmayan ekler "attachment").
    /// </summary>
    public class UploadStorage
    {
        public string Root { get; }

        public UploadStorage(IWebHostEnvironment env, IConfiguration config)
        {
            var configured = config?["Storage:UploadsPath"];
            if (!string.IsNullOrWhiteSpace(configured))
            {
                try
                {
                    Directory.CreateDirectory(configured);
                    Root = configured;
                    return;
                }
                catch
                {
                    // Yapılandırılan yola erişilemedi → wwwroot fallback (site çalışmaya devam etsin).
                }
            }

            var fallback = Path.Combine(env.WebRootPath ?? env.ContentRootPath, "uploads");
            Directory.CreateDirectory(fallback);
            Root = fallback;
        }

        /// <summary>Kök altında alt klasör açar ve mutlak yolu döner.</summary>
        public string EnsureSubDir(params string[] segments)
        {
            var all = new string[segments.Length + 1];
            all[0] = Root;
            for (int i = 0; i < segments.Length; i++) all[i + 1] = segments[i];
            var dir = Path.Combine(all);
            Directory.CreateDirectory(dir);
            return dir;
        }
    }
}
