using System;
using System.Collections.Generic;
using System.IO;

namespace ActivityManagement.Web.Helpers
{
    // Yüklenen dosyalar için güvenlik: yalnız güvenli uzantılar kabul edilir (çalıştırılabilir/markup HARİÇ).
    // .html/.htm/.svg/.js/.exe vb. depolanmış-XSS / kötücül dosya riski taşıdığından reddedilir.
    public static class UploadValidator
    {
        public static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp",           // raster görsel (svg HARİÇ — script taşıyabilir)
            ".pdf", ".txt", ".log", ".csv",
            ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
            ".zip", ".rar", ".7z"
        };

        // Tarayıcıda inline gösterilmesi güvenli olanlar (raster görsel). Diğerleri indirme (attachment) olarak sunulur.
        public static readonly HashSet<string> InlineSafeExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp"
        };

        public static bool IsAllowed(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return false;
            var ext = Path.GetExtension(fileName);
            return !string.IsNullOrEmpty(ext) && AllowedExtensions.Contains(ext);
        }

        public static bool IsInlineSafe(string pathOrName)
        {
            if (string.IsNullOrWhiteSpace(pathOrName)) return false;
            return InlineSafeExtensions.Contains(Path.GetExtension(pathOrName));
        }

        public static string AllowedListText()
            => "png, jpg, gif, webp, pdf, txt, log, csv, doc(x), xls(x), ppt(x), zip, rar";
    }
}
