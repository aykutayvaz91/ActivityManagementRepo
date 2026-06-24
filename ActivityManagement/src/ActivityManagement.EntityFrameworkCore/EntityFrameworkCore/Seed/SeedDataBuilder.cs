using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using ActivityManagement.Entities;

namespace ActivityManagement.EntityFrameworkCore.Seed
{
    public class SeedDataBuilder
    {
        private readonly ActivityManagementDbContext _context;

        public SeedDataBuilder(ActivityManagementDbContext context)
        {
            _context = context;
        }

        public void Create()
        {
            SeedDefaultTenant();
            SeedWorkflowStatuses();
            SeedEmployees();
            SeedWorkItems();   // xlsx: Genel İş Kalemleri + Uygulama VM bazlı iş kalemleri
            SeedActivities();
            _context.SaveChanges();
        }

        private void SeedWorkflowStatuses()
        {
            if (_context.WorkflowStatuses.Any()) return;
            _context.WorkflowStatuses.AddRange(
                new WorkflowStatus { Name = "Beklemede",     Color = "secondary", SortOrder = 1, StatusValue = 0, IsActive = true },
                new WorkflowStatus { Name = "Devam Ediyor",  Color = "primary",   SortOrder = 2, StatusValue = 1, IsActive = true },
                new WorkflowStatus { Name = "Tamamlandı",    Color = "success",   SortOrder = 3, StatusValue = 2, IsActive = true, IsCompletedState = true },
                new WorkflowStatus { Name = "Ertelendi",     Color = "warning",   SortOrder = 4, StatusValue = 4, IsActive = true },
                new WorkflowStatus { Name = "İptal",         Color = "dark",      SortOrder = 5, StatusValue = 3, IsActive = true }
            );
            _context.SaveChanges();
        }

        private void SeedDefaultTenant()
        {
            if (_context.Tenants.IgnoreQueryFilters().Any(t => t.Id == 1)) return;
            _context.Tenants.Add(new Tenant("Default", "Default") { IsActive = true });
            _context.SaveChanges();
        }

        private void SeedEmployees()
        {
            if (_context.Employees.Any()) return;

            var employees = new[]
            {
                new Employee { TenantId = 1, FirstName = "Emin",    LastName = "Akkurt",   Title = "Sistem Takım Lideri",        Department = "Sistem Yönetimi", AppRole = "Admin",        Email = "emin.akkurt@cmit.com.tr",   HireDate = new DateTime(2018, 1, 1), IsActive = true },
                new Employee { TenantId = 1, FirstName = "Aykut",   LastName = "Ayvaz",    Title = "Sistem Uzmanı",              Department = "Sistem Yönetimi", AppRole = "Uzman",        Email = "aykut.ayvaz@cmit.com.tr",   HireDate = new DateTime(2019, 1, 1), IsActive = true },
                new Employee { TenantId = 1, FirstName = "İsmail",  LastName = "Demirtaş", Title = "Sistem Uzmanı",              Department = "Sistem Yönetimi", AppRole = "Uzman",        Email = "ismail.demirtas@cmit.com.tr", HireDate = new DateTime(2019, 6, 1), IsActive = true },
                new Employee { TenantId = 1, FirstName = "Mustafa", LastName = "Keser",    Title = "Sistem Uzmanı",              Department = "Sistem Yönetimi", AppRole = "Uzman",        Email = "mustafa.keser@cmit.com.tr", HireDate = new DateTime(2020, 1, 1), IsActive = true },
                new Employee { TenantId = 1, FirstName = "Mehmet",  LastName = "Güngör",   Title = "Network ve Güvenlik Uzmanı", Department = "Network",         AppRole = "Uzman",        Email = "mehmet.gungor@cmit.com.tr", HireDate = new DateTime(2020, 6, 1), IsActive = true },
                new Employee { TenantId = 1, FirstName = "Bektaş",  LastName = "Ayas",     Title = "Network Uzmanı",             Department = "Network",         AppRole = "Uzman",        Email = "bektas.ayas@cmit.com.tr",   HireDate = new DateTime(2021, 1, 1), IsActive = true }
            };
            _context.Employees.AddRange(employees);
            _context.SaveChanges();
        }

        // xlsx hiyerarşisi: Ana Başlık -> Proje, Alt Başlık -> Category, İş Kalemi -> Görev (TaskItem.Title)
        private static readonly (string Project, (string Category, string[] Items)[] Groups)[] WorkBreakdown = new[]
        {
            ("1. Sistem Yönetimi", new[]
            {
                ("1.1. Sunucu ve Sanallaştırma Yönetimi", new[]
                {
                    "Sunucu konfigürasyon ve kurulumu","Yük dengeleme ve kaynak optimizasyonu","Sistem izleme","Update ve patch yönetimi",
                    "Sanallaştırma ihtiyaç analizi","Hypervisor seçimi ve kurulumu","Sanallaştırma ve kaynak yönetimi","Sorun Teşhis ve Düzeltme"
                }),
                ("1.2. Storage Yönetimi", new[]
                {
                    "Depolama ihtiyaç analizi","Disk yapılandırması","Depolama optimizasyonu (sıkıştırma)","Veri yedekleme ve kurtarma",
                    "Veri erişim politikaları","Performans izleme ve iyileştirme","Depolama alt yapısının esnekliği ve ölçeklenebilirliği"
                })
            }),
            ("2. Yedekleme ve Replikasyon", new[]
            {
                ("2.1. Yedekleme Stratejileri", new[]
                {
                    "Yedekleme ihtiyaç analizi","Yedekleme çözümü seçimi ve donanım ihtiyaçları","Yedekleme planı oluşturma","Yedekleme testleri ve doğrulama prosedürleri",
                    "Yedeklerin güvenliğini sağlama","Yedekleme raporları ve izlenmesi","Sorun Teşhis ve Düzeltme"
                }),
                ("2.2. Replikasyon Stratejileri", new[]
                {
                    "Replikasyon ihtiyaç analizi ve uygunluk testi","Replikasyon teknolojisi seçimi","Replikasyon planı ve zamanlanması","Replikasyon izleme ve raporlama",
                    "Felaket kurtarma planı entegrasyonu","Replikasyon sonrası veri doğrulama işlemleri","Sorun Teşhis ve Düzeltme"
                })
            }),
            ("3. Network Yönetimi", new[]
            {
                ("3.1. Kablolu Ağ Yönetimi", new[]
                {
                    "Topoloji seçimi ve tasarımı","IP adresleme ve subnet planlaması","Switch ve router yapılandırmaları","ACL yönetimi ve QoS politikaları",
                    "Yedekleme ve felaket kurtarma","Performans ve izleme","Sorun teşhis ve düzeltme"
                }),
                ("3.2. Kablosuz Ağ Yönetimi", new[]
                {
                    "Kablosuz erişim noktası seçimi ve yerleşimi","SSID ve güvenlik protokolleri","Misafir ağı ve izolasyon ayarları","İzleme ve performans değerlendirmesi",
                    "Kablosuz ağa özel QoS politikaları","Kablosuz ağ güvenliği ve erişim kontrolü","Sorun teşhis ve düzeltme"
                })
            }),
            ("4. Güvenlik Yönetimi", new[]
            {
                ("4.1. Güvenlik Duvarı", new[]
                {
                    "Güvenlik duvarı ihtiyaç analizi","Güvenlik duvarı seçimi ve kurulumu","Politika ve kuralların yapılandırılması","Log izleme ve alarm kurulumu",
                    "Güncelleme ve yama yönetimi","Sorun teşhis ve düzeltme"
                }),
                ("4.2. Yük Dengeleyici ve WAF", new[]
                {
                    "Yük dengeleyici ihtiyaç analizi","Yük dengeleyici konfigürasyonu","Sağlık kontrolleri ve otomatik failover","Performans izleme ve optimizasyon",
                    "Web uygulama risk değerlendirmesi","WAF seçimi ve kurulumu","Güvenlik politikalarının tanımlanması","Günlük izleme","Sorun teşhis ve düzeltme"
                })
            }),
            ("5. Bulut Hizmetleri", new[]
            {
                ("5.1. Azure IaaS/PaaS", new[]
                {
                    "Kaynak planlaması","Servis seçimi ve kurulumu","Erişim yönetimi (IAM)","Otomatizasyon ve ölçeklendirme",
                    "Maliyet analizi ve optimizasyon","Güvenlik ve uyum politikaları","Sorun teşhis ve düzeltme"
                }),
                ("5.2. Microsoft 365", new[]
                {
                    "Lisanslama ihtiyaç analizi","E-posta yönetimi, konfigürasyonu ve veri göçü","Kullanıcı eğitimi ve kabul süreci","Güvenlik ve uyum politikaları",
                    "Sorun teşhis ve düzeltme","Ekip çalışmasına yönelik araçların devreye alınması"
                })
            }),
            ("6. Eğitim ve Oryantasyon", new[]
            {
                ("6.1. Son Kullanıcı Eğitimi ve Oryantasyon", new[]
                {
                    "İhtiyaç analizi","Eğitim materyali hazırlığı","Eğitim seansları","Oryantasyon kuralları",
                    "Güvenlik farkındalığı","Soru cevap ve geri bildirim","Performans değerlendirme"
                })
            })
        };

        // Sheet 2: Uygulama VM bazlı iş kalemleri
        private static readonly string[] Applications = new[]
        {
            "TDV EBYS","TDV J-Platform (ERP)","TDV Dynamic CRM","TDV Sybase","TDV Milestone Kamera","TDV 3CX",
            "Matbaa Nebim (ERP)","Teyas Mira (Eski ERP)","Teyas IFS (ERP)","Active Directory",
            "ADFS-WAP (Active Directory Federated Service-Web Application Proxy)","Proxy (Azure AD Connect)",
            "File Server'lar","DHCP Server'lar","KMS (Key Management Service)","RDS (Remote Desktop Services)",
            "CA (Certificate Authority)","SCCM (System Center Configuration Manager)","ISE (Cisco Identity Services Engine)",
            "Palo Alto","Citrix","Domain ve bunların SSL sertifikaları takibi","Hosting üzerinde bulunan uygulamaların takibi"
        };

        private void SeedWorkItems()
        {
            if (_context.Projects.Any()) return;

            var employees = _context.Employees.OrderBy(e => e.Id).ToList();
            var leader = employees.First();
            var rand = new Random(42);
            int code = 1;

            // Genel İş Kalemleri -> her ana başlık bir proje
            foreach (var (projectName, groups) in WorkBreakdown)
            {
                var project = new Project
                {
                    TenantId = 1,
                    Name = projectName,
                    Code = $"GEN-{code++:D2}",
                    Description = "Genel altyapı iş kalemleri (xlsx: Genel İş Kalemleri).",
                    StartDate = new DateTime(2024, 1, 1),
                    Status = ProjectStatus.Devam,
                    Priority = 2,
                    ManagerId = leader.Id
                };
                _context.Projects.Add(project);
                _context.SaveChanges();

                foreach (var (category, items) in groups)
                {
                    foreach (var item in items)
                    {
                        var emp = employees[rand.Next(employees.Count)];
                        _context.TaskItems.Add(new TaskItem
                        {
                            TenantId = 1,
                            Title = item,
                            Category = category,
                            ProjectId = project.Id,
                            AssignedEmployeeId = emp.Id,
                            AssignedByEmployeeId = leader.Id,
                            Status = (Entities.TaskStatus)rand.Next(0, 3),
                            Priority = (TaskPriority)rand.Next(0, 4),
                            DueDate = DateTime.Today.AddDays(rand.Next(3, 90)),
                            EstimatedHours = rand.Next(2, 24),
                            CompletionPercentage = rand.Next(0, 11) * 10
                        });
                    }
                }
            }

            // Uygulama VM bazlı iş kalemleri -> tek proje, her uygulama bir görev
            var appProject = new Project
            {
                TenantId = 1,
                Name = "Uygulama VM Bazlı İş Kalemleri",
                Code = "VM-01",
                Description = "Sanal makine/uygulama bazlı yönetim iş kalemleri (xlsx: Uygulama VM bazlı iş kalemleri).",
                StartDate = new DateTime(2024, 1, 1),
                Status = ProjectStatus.Devam,
                Priority = 3,
                ManagerId = leader.Id
            };
            _context.Projects.Add(appProject);
            _context.SaveChanges();

            foreach (var app in Applications)
            {
                var emp = employees[rand.Next(employees.Count)];
                _context.TaskItems.Add(new TaskItem
                {
                    TenantId = 1,
                    Title = app,
                    Category = "Uygulama / VM Takibi",
                    ProjectId = appProject.Id,
                    AssignedEmployeeId = emp.Id,
                    AssignedByEmployeeId = leader.Id,
                    Status = (Entities.TaskStatus)rand.Next(0, 3),
                    Priority = (TaskPriority)rand.Next(0, 4),
                    DueDate = DateTime.Today.AddDays(rand.Next(3, 90)),
                    EstimatedHours = rand.Next(2, 24),
                    CompletionPercentage = rand.Next(0, 11) * 10
                });
            }

            _context.SaveChanges();
        }

        private void SeedActivities()
        {
            if (_context.ActivityLogs.Any()) return;

            var employees = _context.Employees.ToList();
            var rand = new Random(7);
            foreach (var emp in employees)
            {
                for (int i = 0; i < 14; i++)
                {
                    var date = DateTime.Today.AddDays(-i);
                    if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday) continue;
                    _context.ActivityLogs.Add(new ActivityLog
                    {
                        TenantId = 1,
                        EmployeeId = emp.Id,
                        Description = $"{date:dd.MM.yyyy} - {emp.Department} günlük çalışma",
                        ActivityDate = date,
                        HoursSpent = Math.Round((decimal)(4 + rand.NextDouble() * 4), 1),
                        ActivityType = "Operasyon"
                    });
                }
            }
        }
    }
}
