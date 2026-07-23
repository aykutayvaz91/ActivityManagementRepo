using System;
using System.IO;
using System.Text;

namespace ActivityManagement.Logging
{
    // Gün gün dosya logu; bir gün dosyası boyut sınırını aşınca YYYY-MM-DD-1.log, -2.log ... diye bölünür.
    // Loglama uygulamayı asla bozmaz (tüm hatalar yutulur).
    public sealed class DailyRollingLog
    {
        private string _baseDir;
        private readonly object _lock = new object();
        private readonly long _maxBytes;

        public DailyRollingLog(long maxBytes = 5 * 1024 * 1024)
        {
            _maxBytes = maxBytes;
        }

        public void Configure(string baseDir)
        {
            _baseDir = baseDir;
            try { Directory.CreateDirectory(baseDir); } catch { }
        }

        public void WriteLine(string line)
        {
            if (string.IsNullOrEmpty(_baseDir) || string.IsNullOrEmpty(line)) return;
            try
            {
                lock (_lock)
                {
                    var day = DateTime.Now.ToString("yyyy-MM-dd");
                    File.AppendAllText(ResolvePath(day), line + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch { }
        }

        private string ResolvePath(string day)
        {
            var basePath = Path.Combine(_baseDir, day + ".log");
            if (!File.Exists(basePath) || new FileInfo(basePath).Length < _maxBytes)
                return basePath;

            int part = 1;
            string p;
            do { p = Path.Combine(_baseDir, $"{day}-{part}.log"); part++; }
            while (File.Exists(p) && new FileInfo(p).Length >= _maxBytes);
            return p;
        }
    }
}
