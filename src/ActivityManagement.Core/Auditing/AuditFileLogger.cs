using ActivityManagement.Logging;

namespace ActivityManagement.Auditing
{
    // Denetim kayıtlarını dosyaya yazar: logs/audit/2026-07-22.log (büyürse 2026-07-22-1.log ...)
    public static class AuditFileLogger
    {
        private static readonly DailyRollingLog _log = new DailyRollingLog();

        public static void Configure(string baseDir) => _log.Configure(baseDir);
        public static void WriteLine(string line) => _log.WriteLine(line);
    }
}
