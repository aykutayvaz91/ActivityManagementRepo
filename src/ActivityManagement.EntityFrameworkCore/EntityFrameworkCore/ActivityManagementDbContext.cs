using System.Linq;
using Abp.Zero.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ActivityManagement.Entities;

namespace ActivityManagement.EntityFrameworkCore
{
    // AbpZeroDbContext: AbpUsers, AbpRoles, AbpTenants, AbpPermissions vb. tüm Zero
    // tablolarını otomatik mapler. Biz sadece uygulama tablolarını ekliyoruz.
    public class ActivityManagementDbContext : AbpZeroDbContext<Tenant, Role, User, ActivityManagementDbContext>
    {
        public DbSet<Employee> Employees { get; set; }
        public DbSet<Project> Projects { get; set; }
        public DbSet<ProjectEmployee> ProjectEmployees { get; set; }
        public DbSet<TaskItem> TaskItems { get; set; }
        public DbSet<TaskComment> TaskComments { get; set; }
        public DbSet<TaskAttachment> TaskAttachments { get; set; }
        public DbSet<ActivityLog> ActivityLogs { get; set; }
        public DbSet<ActivitySubject> ActivitySubjects { get; set; }
        public DbSet<Responsibility> Responsibilities { get; set; }
        public DbSet<WorkflowStatus> WorkflowStatuses { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<SubCategory> SubCategories { get; set; }
        public DbSet<Team> Teams { get; set; }
        public DbSet<EmailSettings> EmailSettings { get; set; }
        public DbSet<SystemAuditLog> SystemAuditLogs { get; set; }
        public DbSet<SystemAuditLogArchive> SystemAuditLogArchives { get; set; }
        public DbSet<SubCategoryResponsibility> SubCategoryResponsibilities { get; set; }
        public DbSet<ActivityTypeDef> ActivityTypes { get; set; }
        public DbSet<ThemeSettings> ThemeSettings { get; set; }
        public DbSet<AppRoleDef> AppRoles { get; set; }
        public DbSet<RolePageAccess> RolePageAccesses { get; set; }
        public DbSet<ServiceRequest> ServiceRequests { get; set; }
        public DbSet<ServiceRequestComment> ServiceRequestComments { get; set; }
        public DbSet<ServiceRequestAttachment> ServiceRequestAttachments { get; set; }
        public DbSet<IntegrationSettings> IntegrationSettings { get; set; }
        public DbSet<IntegrationSource> IntegrationSources { get; set; }
        public DbSet<Notification> AppNotifications { get; set; }
        public DbSet<NotificationPreference> NotificationPreferences { get; set; }

        public ActivityManagementDbContext(DbContextOptions<ActivityManagementDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Employee>(b =>
            {
                b.ToTable("Employees");
                b.Property(e => e.FirstName).IsRequired().HasMaxLength(64);
                b.Property(e => e.LastName).IsRequired().HasMaxLength(64);
                b.Property(e => e.Title).HasMaxLength(128);
                b.Property(e => e.Department).HasMaxLength(128);
                b.Property(e => e.AppRole).HasMaxLength(32);
                b.Property(e => e.Email).HasMaxLength(256);
                b.Property(e => e.Phone).HasMaxLength(32);
                b.Property(e => e.PhotoUrl).HasMaxLength(512);
                b.HasOne(e => e.Team)
                 .WithMany(t => t.Members)
                 .HasForeignKey(e => e.TeamId)
                 .IsRequired(false)
                 .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<Project>(b =>
            {
                b.ToTable("Projects");
                b.Property(p => p.Name).IsRequired().HasMaxLength(128);
                b.Property(p => p.Code).IsRequired().HasMaxLength(32);
                b.Property(p => p.Description).HasMaxLength(2000);
                b.HasOne(p => p.Manager)
                 .WithMany()
                 .HasForeignKey(p => p.ManagerId)
                 .OnDelete(DeleteBehavior.SetNull);
                b.HasOne(p => p.PrimaryResponsible)
                 .WithMany()
                 .HasForeignKey(p => p.PrimaryResponsibleId)
                 .IsRequired(false)
                 .OnDelete(DeleteBehavior.Restrict);
                b.HasOne(p => p.SecondaryResponsible)
                 .WithMany()
                 .HasForeignKey(p => p.SecondaryResponsibleId)
                 .IsRequired(false)
                 .OnDelete(DeleteBehavior.Restrict);
                b.HasOne(p => p.Category)
                 .WithMany()
                 .HasForeignKey(p => p.CategoryId)
                 .IsRequired(false)
                 .OnDelete(DeleteBehavior.Restrict);
                b.HasOne(p => p.SubCategory)
                 .WithMany()
                 .HasForeignKey(p => p.SubCategoryId)
                 .IsRequired(false)
                 .OnDelete(DeleteBehavior.Restrict);
                b.HasOne(p => p.Team)
                 .WithMany(t => t.Projects)
                 .HasForeignKey(p => p.TeamId)
                 .IsRequired(false)
                 .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<Team>(b =>
            {
                b.ToTable("Teams");
                b.Property(t => t.Name).IsRequired().HasMaxLength(128);
                b.Property(t => t.ShortName).HasMaxLength(32);
                b.Property(t => t.Description).HasMaxLength(2000);
                b.HasOne(t => t.Leader)
                 .WithMany()
                 .HasForeignKey(t => t.LeaderId)
                 .IsRequired(false)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<EmailSettings>(b =>
            {
                b.ToTable("EmailSettings");
                b.Property(s => s.SenderEmail).HasMaxLength(256);
                b.Property(s => s.SenderDisplayName).HasMaxLength(128);
                b.Property(s => s.SmtpHost).HasMaxLength(256);
                b.Property(s => s.SmtpUserName).HasMaxLength(256);
                b.Property(s => s.SmtpPassword).HasMaxLength(1024); // DPAPI şifreli değer uzun (base64+prefix)
            });

            modelBuilder.Entity<ProjectEmployee>(b =>
            {
                b.ToTable("ProjectEmployees");
                b.HasOne(pe => pe.Project)
                 .WithMany(p => p.ProjectEmployees)
                 .HasForeignKey(pe => pe.ProjectId);
                b.HasOne(pe => pe.Employee)
                 .WithMany(e => e.ProjectEmployees)
                 .HasForeignKey(pe => pe.EmployeeId);
            });

            modelBuilder.Entity<TaskItem>(b =>
            {
                b.ToTable("TaskItems");
                b.Property(t => t.Title).IsRequired().HasMaxLength(256);
                b.Property(t => t.Description).HasMaxLength(2000);
                b.Property(t => t.Category).HasMaxLength(256);
                b.Property(t => t.GroupName).HasMaxLength(64);
                b.Property(t => t.EstimatedHours).HasPrecision(18, 2);
                b.Property(t => t.ActualHours).HasPrecision(18, 2);
                b.HasOne(t => t.Project)
                 .WithMany(p => p.Tasks)
                 .HasForeignKey(t => t.ProjectId)
                 .OnDelete(DeleteBehavior.SetNull);
                b.HasOne(t => t.AssignedEmployee)
                 .WithMany(e => e.AssignedTasks)
                 .HasForeignKey(t => t.AssignedEmployeeId)
                 .OnDelete(DeleteBehavior.SetNull);
                b.HasOne(t => t.SecondaryEmployee)
                 .WithMany()
                 .HasForeignKey(t => t.SecondaryEmployeeId)
                 .IsRequired(false)
                 .OnDelete(DeleteBehavior.NoAction);
                b.HasOne(t => t.AssignedByEmployee)
                 .WithMany()
                 .HasForeignKey(t => t.AssignedByEmployeeId)
                 .OnDelete(DeleteBehavior.Restrict);
                b.HasOne(t => t.SubCategory)
                 .WithMany(sc => sc.Tasks)
                 .HasForeignKey(t => t.SubCategoryId)
                 .IsRequired(false)
                 .OnDelete(DeleteBehavior.Restrict);
                b.HasOne(t => t.Team)
                 .WithMany(tm => tm.Tasks)
                 .HasForeignKey(t => t.TeamId)
                 .IsRequired(false)
                 .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<Category>(b =>
            {
                b.ToTable("Categories");
                b.Property(c => c.Name).IsRequired().HasMaxLength(256);
                b.Property(c => c.Description).HasMaxLength(2000);
                b.HasOne(c => c.ResponsibleEmployee1)
                 .WithMany()
                 .HasForeignKey(c => c.ResponsibleEmployee1Id)
                 .IsRequired(false)
                 .OnDelete(DeleteBehavior.Restrict);
                b.HasOne(c => c.ResponsibleEmployee2)
                 .WithMany()
                 .HasForeignKey(c => c.ResponsibleEmployee2Id)
                 .IsRequired(false)
                 .OnDelete(DeleteBehavior.Restrict);
                b.HasOne(c => c.Team)
                 .WithMany()
                 .HasForeignKey(c => c.TeamId)
                 .IsRequired(false)
                 .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<SubCategory>(b =>
            {
                b.ToTable("SubCategories");
                b.Property(sc => sc.Name).IsRequired().HasMaxLength(256);
                b.Property(sc => sc.Description).HasMaxLength(2000);
                b.HasOne(sc => sc.Category)
                 .WithMany(c => c.SubCategories)
                 .HasForeignKey(sc => sc.CategoryId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<TaskComment>(b =>
            {
                b.ToTable("TaskComments");
                b.HasOne(c => c.TaskItem)
                 .WithMany(t => t.Comments)
                 .HasForeignKey(c => c.TaskItemId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<TaskAttachment>(b =>
            {
                b.ToTable("TaskAttachments");
                b.HasOne(a => a.TaskItem)
                 .WithMany(t => t.Attachments)
                 .HasForeignKey(a => a.TaskItemId)
                 .OnDelete(DeleteBehavior.Cascade);
                b.HasOne(a => a.TaskComment)
                 .WithMany(c => c.Attachments)
                 .HasForeignKey(a => a.TaskCommentId)
                 .IsRequired(false)
                 .OnDelete(DeleteBehavior.NoAction); // SetNull çoklu cascade yolu hatası verir (TaskItem->Comment->Attachment)
            });

            modelBuilder.Entity<ActivityLog>(b =>
            {
                b.ToTable("ActivityLogs");
                b.Property(a => a.HoursSpent).HasPrecision(18, 2);
                b.HasOne(a => a.Employee)
                 .WithMany(e => e.ActivityLogs)
                 .HasForeignKey(a => a.EmployeeId)
                 .OnDelete(DeleteBehavior.Restrict);
                b.HasOne(a => a.TaskItem)
                 .WithMany(t => t.ActivityLogs)
                 .HasForeignKey(a => a.TaskItemId)
                 .OnDelete(DeleteBehavior.SetNull);
                b.HasOne(a => a.Project)
                 .WithMany()
                 .HasForeignKey(a => a.ProjectId)
                 .OnDelete(DeleteBehavior.SetNull);
                b.HasOne(a => a.ActivitySubject)
                 .WithMany(s => s.Logs)
                 .HasForeignKey(a => a.ActivitySubjectId)
                 .IsRequired(false)
                 .OnDelete(DeleteBehavior.SetNull);
                b.HasOne(a => a.ServiceRequest)
                 .WithMany(s => s.Logs)
                 .HasForeignKey(a => a.ServiceRequestId)
                 .IsRequired(false)
                 .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<ActivitySubject>(b =>
            {
                b.ToTable("ActivitySubjects");
                b.Property(s => s.Title).IsRequired().HasMaxLength(256);
                b.Property(s => s.ActivityType).HasMaxLength(64);
                b.Property(s => s.Description).HasMaxLength(2000);
                b.HasOne(s => s.Category)
                 .WithMany()
                 .HasForeignKey(s => s.CategoryId)
                 .IsRequired(false)
                 .OnDelete(DeleteBehavior.Restrict);
                b.HasOne(s => s.SubCategory)
                 .WithMany()
                 .HasForeignKey(s => s.SubCategoryId)
                 .IsRequired(false)
                 .OnDelete(DeleteBehavior.Restrict);
                b.HasOne(s => s.CreatedByLeader)
                 .WithMany()
                 .HasForeignKey(s => s.CreatedByLeaderId)
                 .IsRequired(false)
                 .OnDelete(DeleteBehavior.Restrict);
                b.HasOne(s => s.AssignedEmployee)
                 .WithMany()
                 .HasForeignKey(s => s.AssignedEmployeeId)
                 .IsRequired(false)
                 .OnDelete(DeleteBehavior.Restrict);
                b.HasOne(s => s.Team)
                 .WithMany()
                 .HasForeignKey(s => s.TeamId)
                 .IsRequired(false)
                 .OnDelete(DeleteBehavior.SetNull);
                b.HasOne(s => s.Project)
                 .WithMany()
                 .HasForeignKey(s => s.ProjectId)
                 .IsRequired(false)
                 .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<ServiceRequest>(b =>
            {
                b.ToTable("ServiceRequests");
                b.Property(s => s.Title).IsRequired().HasMaxLength(512);
                b.Property(s => s.ActivityType).HasMaxLength(64);
                b.Property(s => s.Description).HasColumnType("nvarchar(max)"); // portal HTML açıklaması uzun olabilir
                b.Property(s => s.ExternalRef).HasMaxLength(128);
                b.Property(s => s.ExternalUrl).HasMaxLength(1024);
                b.Property(s => s.RequesterName).HasMaxLength(256);
                b.Property(s => s.RequesterEmail).HasMaxLength(256);
                b.Property(s => s.ExtraInfo).HasMaxLength(2000);
                // (B8) (Source, ExternalRef) idempotent upsert için FILTERED UNIQUE — yarış/çift kayıt engeli.
                // ExternalRef null (manuel talep) ve soft-deleted satırlar hariç (silinmiş satır re-import'u bloklamasın).
                b.HasIndex(s => new { s.Source, s.ExternalRef })
                 .IsUnique()
                 .HasFilter("[ExternalRef] IS NOT NULL AND [IsDeleted] = 0");
                b.HasOne(s => s.AssignedEmployee)
                 .WithMany()
                 .HasForeignKey(s => s.AssignedEmployeeId)
                 .IsRequired(false)
                 .OnDelete(DeleteBehavior.SetNull);
                b.HasOne(s => s.SecondaryEmployee)
                 .WithMany()
                 .HasForeignKey(s => s.SecondaryEmployeeId)
                 .IsRequired(false)
                 .OnDelete(DeleteBehavior.NoAction);
                b.HasOne(s => s.Team)
                 .WithMany()
                 .HasForeignKey(s => s.TeamId)
                 .IsRequired(false)
                 .OnDelete(DeleteBehavior.SetNull);
                b.HasOne(s => s.Category)
                 .WithMany()
                 .HasForeignKey(s => s.CategoryId)
                 .IsRequired(false)
                 .OnDelete(DeleteBehavior.Restrict);
                b.HasOne(s => s.SubCategory)
                 .WithMany()
                 .HasForeignKey(s => s.SubCategoryId)
                 .IsRequired(false)
                 .OnDelete(DeleteBehavior.Restrict);
                b.HasOne(s => s.Project)
                 .WithMany()
                 .HasForeignKey(s => s.ProjectId)
                 .IsRequired(false)
                 .OnDelete(DeleteBehavior.SetNull);
            });

            // (C12) Talep portal yorumları — aynası; (ServiceRequestId, ExternalCommentId) dedup unique.
            modelBuilder.Entity<ServiceRequestComment>(b =>
            {
                b.ToTable("ServiceRequestComments");
                b.Property(c => c.ExternalCommentId).HasMaxLength(128);
                b.Property(c => c.AuthorName).HasMaxLength(256);
                b.Property(c => c.AuthorEmail).HasMaxLength(256);
                b.Property(c => c.Body).HasColumnType("nvarchar(max)");
                b.HasIndex(c => new { c.ServiceRequestId, c.ExternalCommentId })
                 .IsUnique().HasFilter("[ExternalCommentId] IS NOT NULL");
                b.HasOne(c => c.ServiceRequest).WithMany(r => r.Comments)
                 .HasForeignKey(c => c.ServiceRequestId).OnDelete(DeleteBehavior.Cascade);
            });

            // (C12) Talep portal dosya ekleri — aynası; (ServiceRequestId, ExternalAttachmentId) dedup unique.
            modelBuilder.Entity<ServiceRequestAttachment>(b =>
            {
                b.ToTable("ServiceRequestAttachments");
                b.Property(a => a.ExternalAttachmentId).HasMaxLength(128);
                b.Property(a => a.FileName).HasMaxLength(512);
                b.Property(a => a.Url).HasMaxLength(1024);
                b.Property(a => a.ContentType).HasMaxLength(256);
                b.HasIndex(a => new { a.ServiceRequestId, a.ExternalAttachmentId })
                 .IsUnique().HasFilter("[ExternalAttachmentId] IS NOT NULL");
                b.HasOne(a => a.ServiceRequest).WithMany(r => r.Attachments)
                 .HasForeignKey(a => a.ServiceRequestId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<IntegrationSettings>(b =>
            {
                b.ToTable("IntegrationSettings");
                b.Property(s => s.InboundApiKey).HasMaxLength(1024); // DPAPI şifreli değer uzun
            });

            modelBuilder.Entity<IntegrationSource>(b =>
            {
                b.ToTable("IntegrationSources");
                b.Property(s => s.BaseUrl).HasMaxLength(512);
                b.Property(s => s.ApiKey).HasMaxLength(1024); // DPAPI şifreli değer uzun
                b.Property(s => s.AuthHeader).HasMaxLength(64);
                b.Property(s => s.AuthScheme).HasMaxLength(32);
                b.Property(s => s.Filter).HasMaxLength(1024);
                b.Property(s => s.LastResult).HasMaxLength(1024);
                b.HasIndex(s => s.Source).IsUnique();
            });

            modelBuilder.Entity<Notification>(b =>
            {
                b.ToTable("Notifications");
                b.Property(n => n.Title).HasMaxLength(256);
                b.Property(n => n.Message).HasMaxLength(1024);
                b.Property(n => n.Link).HasMaxLength(512);
                b.Property(n => n.Icon).HasMaxLength(64);
                b.Property(n => n.Severity).HasMaxLength(16);
                b.HasIndex(n => new { n.RecipientEmployeeId, n.IsRead });
            });

            modelBuilder.Entity<NotificationPreference>(b =>
            {
                b.ToTable("NotificationPreferences");
                b.Property(p => p.MutedTypes).HasMaxLength(256);
                b.HasIndex(p => p.EmployeeId).IsUnique();
            });

            modelBuilder.Entity<WorkflowStatus>(b =>
            {
                b.ToTable("WorkflowStatuses");
                b.Property(s => s.Name).IsRequired().HasMaxLength(64);
                b.Property(s => s.Color).HasMaxLength(32);
            });

            modelBuilder.Entity<ActivityTypeDef>(b =>
            {
                b.ToTable("ActivityTypes");
                b.Property(s => s.Name).IsRequired().HasMaxLength(64);
                b.HasIndex(s => s.Name);
            });

            modelBuilder.Entity<AppRoleDef>(b =>
            {
                b.ToTable("AppRoles");
                b.Property(s => s.Name).IsRequired().HasMaxLength(64);
                b.HasIndex(s => s.Name).IsUnique();
            });

            modelBuilder.Entity<RolePageAccess>(b =>
            {
                b.ToTable("RolePageAccesses");
                b.Property(s => s.RoleName).IsRequired().HasMaxLength(64);
                b.Property(s => s.PageKey).IsRequired().HasMaxLength(64);
                b.HasIndex(s => new { s.RoleName, s.PageKey }).IsUnique();
            });

            modelBuilder.Entity<Responsibility>(b =>
            {
                b.ToTable("Responsibilities");
                b.HasOne(r => r.Employee)
                 .WithMany(e => e.Responsibilities)
                 .HasForeignKey(r => r.EmployeeId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<SubCategoryResponsibility>(b =>
            {
                b.ToTable("SubCategoryResponsibilities");
                b.HasOne(r => r.SubCategory).WithMany().HasForeignKey(r => r.SubCategoryId).OnDelete(DeleteBehavior.Cascade);
                b.HasOne(r => r.Employee).WithMany().HasForeignKey(r => r.EmployeeId).OnDelete(DeleteBehavior.Restrict);
                b.HasOne(r => r.AssignedByTeamLeader).WithMany().HasForeignKey(r => r.AssignedByTeamLeaderId).IsRequired(false).OnDelete(DeleteBehavior.NoAction);
                b.HasIndex(r => new { r.SubCategoryId, r.EmployeeId }).IsUnique();
            });

            modelBuilder.Entity<SystemAuditLog>(b =>
            {
                b.ToTable("SystemAuditLogs");
                b.Property(a => a.ActionType).HasMaxLength(16);
                b.Property(a => a.EntityName).HasMaxLength(128);
                b.Property(a => a.EntityId).HasMaxLength(64);
                b.Property(a => a.UserName).HasMaxLength(256);
                b.Property(a => a.ClientIpAddress).HasMaxLength(64);
                b.HasIndex(a => a.ExecutionTime);
                b.HasIndex(a => a.EntityName);
            });

            modelBuilder.Entity<SystemAuditLogArchive>(b =>
            {
                b.ToTable("SystemAuditLogArchives");
                b.Property(a => a.ActionType).HasMaxLength(16);
                b.Property(a => a.EntityName).HasMaxLength(128);
                b.Property(a => a.EntityId).HasMaxLength(64);
                b.Property(a => a.UserName).HasMaxLength(256);
                b.Property(a => a.ClientIpAddress).HasMaxLength(64);
                b.HasIndex(a => a.ExecutionTime);
                b.HasIndex(a => a.EntityName);
                b.HasIndex(a => a.OriginalId);
            });
        }

        // --- Denetim (Audit) interceptor: tüm Create/Update/Delete otomatik loglanır ---
        private bool _auditing;

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            if (_auditing) return base.SaveChanges(acceptAllChangesOnSuccess);
            var entries = CaptureAuditEntries();
            var result = base.SaveChanges(acceptAllChangesOnSuccess);
            WriteAuditLogs(entries, acceptAllChangesOnSuccess);
            return result;
        }

        public override async System.Threading.Tasks.Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, System.Threading.CancellationToken cancellationToken = default)
        {
            if (_auditing) return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
            var entries = CaptureAuditEntries();
            var result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
            WriteAuditLogs(entries, acceptAllChangesOnSuccess);
            return result;
        }

        private class PendingAudit
        {
            public Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry Entry;
            public string Action;
            public string EntityName;
            public System.Collections.Generic.Dictionary<string, object> Original;
            public System.Collections.Generic.Dictionary<string, object> New;
        }

        private System.Collections.Generic.List<PendingAudit> CaptureAuditEntries()
        {
            var list = new System.Collections.Generic.List<PendingAudit>();
            foreach (var entry in ChangeTracker.Entries())
            {
                if (entry.Entity is SystemAuditLog) continue;
                if (entry.Entity is SystemAuditLogArchive) continue; // arşivleme kendini loglamaz (özyineleme/gürültü önlenir)
                if (entry.Entity is Notification) continue;           // in-app bildirim geçici UX kaydı → denetlenmez (audit büyümesini de azaltır)
                if (entry.State != EntityState.Added && entry.State != EntityState.Modified && entry.State != EntityState.Deleted) continue;

                var pa = new PendingAudit
                {
                    Entry = entry,
                    EntityName = entry.Metadata.ClrType.Name,
                    Original = new System.Collections.Generic.Dictionary<string, object>(),
                    New = new System.Collections.Generic.Dictionary<string, object>()
                };

                if (entry.State == EntityState.Added)
                {
                    pa.Action = "Create";
                    foreach (var p in entry.Properties)
                        if (!p.Metadata.IsPrimaryKey()) pa.New[p.Metadata.Name] = Trim(p.CurrentValue);
                }
                else if (entry.State == EntityState.Deleted)
                {
                    pa.Action = "Delete";
                    foreach (var p in entry.Properties)
                        pa.Original[p.Metadata.Name] = Trim(p.OriginalValue);
                }
                else // Modified
                {
                    pa.Action = "Update";
                    foreach (var p in entry.Properties)
                    {
                        if (!p.IsModified) continue;
                        pa.Original[p.Metadata.Name] = Trim(p.OriginalValue);
                        pa.New[p.Metadata.Name] = Trim(p.CurrentValue);
                    }
                    if (pa.New.Count == 0) continue; // gerçek değişiklik yok
                }
                list.Add(pa);
            }
            return list;
        }

        private static object Trim(object v)
        {
            if (v is string s && s.Length > 1000) return s.Substring(0, 1000) + "…";
            return v;
        }

        private void WriteAuditLogs(System.Collections.Generic.List<PendingAudit> entries, bool acceptAllChangesOnSuccess)
        {
            if (entries == null || entries.Count == 0) return;
            var user = ActivityManagement.Auditing.AuditUserContext.Current;
            var now = System.DateTime.Now;
            var jsonOpts = new System.Text.Json.JsonSerializerOptions { WriteIndented = false };

            foreach (var pa in entries)
            {
                string entityId = null;
                try { entityId = pa.Entry.Metadata.FindPrimaryKey()?.Properties?.Select(p => pa.Entry.Property(p.Name).CurrentValue?.ToString()).FirstOrDefault(); } catch { }

                var original = pa.Original.Count > 0 ? System.Text.Json.JsonSerializer.Serialize(pa.Original, jsonOpts) : null;
                var newVals = pa.New.Count > 0 ? System.Text.Json.JsonSerializer.Serialize(pa.New, jsonOpts) : null;

                SystemAuditLogs.Add(new SystemAuditLog
                {
                    TenantId = 1,
                    UserId = user?.UserId,
                    UserName = user?.UserName ?? "sistem",
                    ExecutionTime = now,
                    ClientIpAddress = user?.Ip,
                    ActionType = pa.Action,
                    EntityName = pa.EntityName,
                    EntityId = entityId,
                    OriginalValues = original,
                    NewValues = newVals
                });

                // Dosyaya da yaz (logs/audit/gün gün) — satır başında tam tarih-saat (arama kolaylığı)
                var line = $"{now:yyyy-MM-dd HH:mm:ss} | {pa.Action,-6} | {pa.EntityName}#{entityId} | " +
                           $"user={user?.UserName ?? "sistem"}(id={user?.UserId}) ip={user?.Ip}" +
                           (original != null ? $" | OLD={original}" : "") +
                           (newVals != null ? $" | NEW={newVals}" : "");
                ActivityManagement.Auditing.AuditFileLogger.WriteLine(line);
            }

            _auditing = true;
            try { base.SaveChanges(acceptAllChangesOnSuccess); }
            finally { _auditing = false; }
        }
    }
}
