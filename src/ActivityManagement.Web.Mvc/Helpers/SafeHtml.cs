using System.Net;
using System.Text.RegularExpressions;

namespace ActivityManagement.Web.Helpers
{
    // Dış portallardan (destek.cmit.com.tr vb.) gelen açıklama HTML içerebilir. Ham render XSS riskidir;
    // burada beyaz-liste dışı tehlikeli tag/öznitelikler temizlenir, biçimlendirme (p, br, strong, a...) korunur.
    // Düz metinse HtmlEncode + satır sonu <br/> uygulanır.
    public static class SafeHtml
    {
        private const RegexOptions O = RegexOptions.IgnoreCase | RegexOptions.Singleline;
        // <script>/<style> bloklarını (içeriğiyle) sil
        private static readonly Regex ScriptStyle = new Regex(@"<\s*(script|style)\b[^>]*>.*?<\s*/\s*\1\s*>", O);
        // Tehlikeli tag'ler (açılış/kapanış)
        private static readonly Regex DangerTags = new Regex(@"<\s*/?\s*(script|style|iframe|object|embed|form|input|button|textarea|select|option|link|meta|base|svg|math|applet|frame|frameset|title)\b[^>]*>", O);
        // on* olay öznitelikleri (onclick=...)
        private static readonly Regex EventAttrs = new Regex(@"\son\w+\s*=\s*(""[^""]*""|'[^']*'|[^\s>]+)", O);
        // href/src içinde javascript:/vbscript: HER ZAMAN; data: yalnız data:image/ DEĞİLSE engellenir
        // (base64 ekran görüntüsü <img src="data:image/png;base64,..."> korunur; data:text/html vb. temizlenir).
        private static readonly Regex BadScheme = new Regex(@"(href|src|xlink:href)\s*=\s*(['""]?)\s*(?:javascript\s*:|vbscript\s*:|data\s*:(?!\s*image/))", O);
        // İçerik HTML mi (bir tag açılışı var mı)
        private static readonly Regex LooksLikeHtml = new Regex(@"<\s*[a-zA-Z!/]", O);

        public static string RenderDescription(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "";
            var s = input;

            // Düz metin: encode + satır sonlarını <br/> yap
            if (!LooksLikeHtml.IsMatch(s))
                return WebUtility.HtmlEncode(s).Replace("\r\n", "\n").Replace("\n", "<br/>");

            // HTML: tehlikeli kısımları temizle, biçimlendirmeyi koru
            s = ScriptStyle.Replace(s, "");
            s = DangerTags.Replace(s, "");
            s = EventAttrs.Replace(s, "");
            s = BadScheme.Replace(s, "$1=$2#");
            return s;
        }
    }
}
