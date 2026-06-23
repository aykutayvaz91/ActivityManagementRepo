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
        public DbSet<RoutineTask> RoutineTasks { get; set; }
        public DbSet<Responsibility> Responsibilities { get; set; }

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
                b.HasOne(t => t.AssignedByEmployee)
                 .WithMany()
                 .HasForeignKey(t => t.AssignedByEmployeeId)
                 .OnDelete(DeleteBehavior.Restrict);
                b.HasOne(t => t.RoutineTask)
                 .WithMany(r => r.GeneratedTasks)
                 .HasForeignKey(t => t.RoutineTaskId)
                 .OnDelete(DeleteBehavior.SetNull);
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
            });

            modelBuilder.Entity<RoutineTask>(b =>
            {
                b.ToTable("RoutineTasks");
                b.Property(r => r.Title).IsRequired().HasMaxLength(256);
                b.Property(r => r.EstimatedHours).HasPrecision(18, 2);
                b.HasOne(r => r.Employee)
                 .WithMany(e => e.RoutineTasks)
                 .HasForeignKey(r => r.EmployeeId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Responsibility>(b =>
            {
                b.ToTable("Responsibilities");
                b.HasOne(r => r.Employee)
                 .WithMany(e => e.Responsibilities)
                 .HasForeignKey(r => r.EmployeeId)
                 .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
