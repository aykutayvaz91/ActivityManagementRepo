using System;
using System.Collections.Concurrent;
using System.IO;
using PdfSharp.Fonts;

namespace ActivityManagement.Web.Helpers
{
    // PDFsharp 6 (core) net8.0 için font çözücü: Arial'ı Windows Fonts klasöründen (TTF) yükler.
    // Türkçe karakter desteği Arial'da mevcut. Tek sefer GlobalFontSettings'e kaydedilir.
    public sealed class PdfFontResolver : IFontResolver
    {
        public static readonly PdfFontResolver Instance = new PdfFontResolver();
        private static readonly ConcurrentDictionary<string, byte[]> _cache = new();
        private static readonly string FontsDir =
            Environment.GetFolderPath(Environment.SpecialFolder.Fonts); // genelde C:\Windows\Fonts

        private const string Regular = "Arial#Regular";
        private const string Bold = "Arial#Bold";

        public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
            => new FontResolverInfo(isBold ? Bold : Regular);

        public byte[] GetFont(string faceName)
        {
            return _cache.GetOrAdd(faceName, key =>
            {
                var file = key == Bold ? "arialbd.ttf" : "arial.ttf";
                var path = Path.Combine(FontsDir, file);
                if (!File.Exists(path))
                {
                    // Yedek: normal Arial
                    path = Path.Combine(FontsDir, "arial.ttf");
                }
                return File.ReadAllBytes(path);
            });
        }
    }
}
