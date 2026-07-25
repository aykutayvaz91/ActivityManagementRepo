using System.Collections.Generic;

namespace ActivityManagement.Authorization
{
    // Rol × Sayfa erişim matrisinin sayfa listesi. Menü/erişim burada tanımlı anahtarlarla kontrol edilir.
    public static class PageCatalog
    {
        public class PageDef
        {
            public string Key { get; set; }
            public string Title { get; set; }
            public PageDef(string key, string title) { Key = key; Title = title; }
        }

        public static readonly List<PageDef> Pages = new List<PageDef>
        {
            new PageDef("Dashboard",  "Ana Sayfa (Panel)"),
            new PageDef("Employees",  "Personeller"),
            new PageDef("Projects",   "Projeler"),
            new PageDef("Work",       "İşlerim (Genel Bakış)"),
            new PageDef("Requests",   "Talepler"),
            new PageDef("Tasks",      "Görevler (Admin Liste)"),
            new PageDef("Board",      "Pano"),
            new PageDef("MyTasks",    "Görevlerim"),
            new PageDef("Activities", "Faaliyetler"),
            new PageDef("DailyEffort","Günlük Efor"),
            new PageDef("TaskQuery",  "Görev Sorgula"),
            new PageDef("Reports",    "Raporlar"),
            new PageDef("Admin",      "Admin Panel"),
        };

        // Sistem rolleri için varsayılan erişim (seed + eksik kayıt fallback).
        // Admin: her sayfa (kod içinde ayrıca her zaman true). Aşağıda Admin dahil edilmez; Admin özel.
        public static readonly Dictionary<string, HashSet<string>> DefaultAccess = new Dictionary<string, HashSet<string>>
        {
            ["Admin"] = new HashSet<string> { "Dashboard","Employees","Projects","Work","Requests","Tasks","Board","MyTasks","Activities","DailyEffort","TaskQuery","Reports","Admin" },
            // Manager: tüm takımları görür (admin gibi geniş), ama admin-özel config (tema/entegrasyon/rol) YOK → "Admin" sayfası yok
            ["Manager"] = new HashSet<string> { "Dashboard","Employees","Projects","Work","Requests","Board","MyTasks","Activities","DailyEffort","TaskQuery","Reports" },
            ["TakımLideri"] = new HashSet<string> { "Dashboard","Employees","Projects","Work","Requests","Board","MyTasks","Activities","DailyEffort","TaskQuery","Reports","Admin" },
            ["Uzman"] = new HashSet<string> { "Dashboard","Employees","Projects","Work","Requests","Board","MyTasks","Activities","DailyEffort","TaskQuery","Reports" },
        };

        public static readonly (string Name, string Display)[] SystemRoles = new[]
        {
            ("Admin", "Admin"),
            ("Manager", "Manager (Tüm Takımlar)"),
            ("TakımLideri", "Takım Lideri"),
            ("Uzman", "Uzman"),
        };
    }
}
