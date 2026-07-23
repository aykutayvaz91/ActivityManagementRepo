using System;

namespace ActivityManagement.Logging
{
    // Uygulama hatalarını dosyaya yazar: logs/errors/2026-07-22.log (satır başında tam tarih-saat).
    public static class ErrorLog
    {
        private static readonly DailyRollingLog _log = new DailyRollingLog();

        public static void Configure(string baseDir) => _log.Configure(baseDir);

        public static void Write(Exception ex, string context = null)
        {
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | {context}" +
                       $"{Environment.NewLine}{ex}{Environment.NewLine}{new string('-', 80)}";
            _log.WriteLine(line);
        }

        public static void Write(string message, string context = null)
        {
            _log.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | {context} | {message}");
        }
    }
}
