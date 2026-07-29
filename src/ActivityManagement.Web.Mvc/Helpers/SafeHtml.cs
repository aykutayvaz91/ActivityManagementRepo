using System;
using System.Net;
using System.Text.RegularExpressions;
using Ganss.Xss;

namespace ActivityManagement.Web.Helpers
{
    // Dış portallardan (destek.cmit.com.tr vb.) ve müşteri yorumlarından gelen HTML XSS riski taşır.
    // Regex tabanlı temizleme bypass edilebilir (ör. <img/src=x/onerror=...>); bu yüzden gerçek bir
    // beyaz-liste HTML sanitizer'ı (Ganss HtmlSanitizer) kullanılır. Biçimlendirme (p, br, strong, a, img...)
    // korunur; script/iframe/olay öznitelikleri/tehlikeli şemalar temizlenir. Ekran görüntüsü için yalnız
    // data:image/* şemasına izin verilir (data:text/html vb. reddedilir).
    public static class SafeHtml
    {
        private static readonly Regex LooksLikeHtml =
            new(@"<\s*[a-zA-Z!/]", RegexOptions.IgnoreCase | RegexOptions.Singleline);

        private static readonly HtmlSanitizer _sanitizer = CreateSanitizer();

        private static HtmlSanitizer CreateSanitizer()
        {
            var s = new HtmlSanitizer();
            // Base64 ekran görüntüsü (Quill Ctrl+V) için data: şemasına izin — ama yalnız data:image/*
            s.AllowedSchemes.Add("data");
            s.FilterUrl += (sender, e) =>
            {
                var u = (e.OriginalUrl ?? "").TrimStart();
                if (u.StartsWith("data:", StringComparison.OrdinalIgnoreCase) &&
                    !u.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
                    e.SanitizedUrl = null; // data:text/html gibi tehlikeli data: reddedilir
            };
            return s;
        }

        public static string RenderDescription(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "";
            // Düz metin: encode + satır sonlarını <br/> yap
            if (!LooksLikeHtml.IsMatch(input))
                return WebUtility.HtmlEncode(input).Replace("\r\n", "\n").Replace("\n", "<br/>");
            // HTML: beyaz-liste sanitizer
            return _sanitizer.Sanitize(input);
        }
    }
}
