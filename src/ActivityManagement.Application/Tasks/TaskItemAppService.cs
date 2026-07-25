using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Abp.Application.Services.Dto;
using Abp.Authorization;
using Abp.Domain.Repositories;
using Abp.Linq.Extensions;
using Abp.UI;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ActivityManagement.Authorization;
using ActivityManagement.Entities;
using ActivityManagement.Tasks.Dto;

namespace ActivityManagement.Tasks
{
    public class TaskItemAppService : ActivityManagementAppServiceBase, ITaskItemAppService
    {
        private readonly IRepository<TaskItem, long> _taskRepository;
        private readonly IRepository<TaskComment, long> _commentRepository;
        private readonly IRepository<TaskAttachment, long> _attachmentRepository;
        private readonly IRepository<Employee, long> _employeeRepository;
        private readonly IRepository<SubCategory, long> _subCategoryRepository;
        private readonly IRepository<Project, long> _projectRepository;
        private readonly IRepository<Team, long> _teamRepository;
        private readonly IRepository<ActivityLog, long> _activityLogRepository;
        private readonly ActivityManagement.Notifications.IAppEmailSender _emailSender;
        private readonly ActivityManagement.Notifications.INotificationManager _notificationManager;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public TaskItemAppService(
            IRepository<TaskItem, long> taskRepository,
            IRepository<TaskComment, long> commentRepository,
            IRepository<TaskAttachment, long> attachmentRepository,
            IRepository<Employee, long> employeeRepository,
            IRepository<SubCategory, long> subCategoryRepository,
            IRepository<Project, long> projectRepository,
            IRepository<Team, long> teamRepository,
            IRepository<ActivityLog, long> activityLogRepository,
            ActivityManagement.Notifications.IAppEmailSender emailSender,
            ActivityManagement.Notifications.INotificationManager notificationManager,
            IHttpContextAccessor httpContextAccessor)
        {
            _taskRepository = taskRepository;
            _commentRepository = commentRepository;
            _attachmentRepository = attachmentRepository;
            _employeeRepository = employeeRepository;
            _subCategoryRepository = subCategoryRepository;
            _projectRepository = projectRepository;
            _teamRepository = teamRepository;
            _activityLogRepository = activityLogRepository;
            _emailSender = emailSender;
            _notificationManager = notificationManager;
            _httpContextAccessor = httpContextAccessor;
        }

        // Görev "Tamamlandı" yapılmadan önce en az bir efor (harcanan süre) girilmiş olmalı.
        // Boş görev tamamlanamaz — kullanıcıdan efor girmesi istenir.
        private async Task EnsureHasEffortForCompletionAsync(long taskId)
        {
            var hours = await _activityLogRepository.GetAll()
                .Where(l => l.TaskItemId == taskId)
                .Select(l => (decimal?)l.HoursSpent).SumAsync() ?? 0m;
            if (hours <= 0m)
                throw new UserFriendlyException("Görevi 'Tamamlandı' yapmadan önce efor (harcanan süre) girmelisiniz. Lütfen görev üzerinden efor ekleyin.");
        }

        // İzin durumu (tarih-duyarlı): IsOnLeave işaretli VE (bitiş yok veya bugüne/ileriye) ise izinli sayılır.
        private static bool IsOnLeaveNow(Employee e) =>
            e != null && e.IsOnLeave && (!e.LeaveEndDate.HasValue || e.LeaveEndDate.Value.Date >= DateTime.Today);

        // Mevcut kullanıcının rolü ve çalışan kimliği (cookie claim'lerinden - DB sorgusu yok)
        private (string Role, string Email, long? EmployeeId) CurrentContext()
        {
            var user = _httpContextAccessor.HttpContext?.User;
            var role = user?.FindFirst(ClaimTypes.Role)?.Value ?? "Uzman";
            var email = user?.FindFirst(ClaimTypes.Email)?.Value
                        ?? user?.FindFirst(ClaimTypes.Name)?.Value;
            var empIdStr = user?.FindFirst("EmployeeId")?.Value;
            long? empId = long.TryParse(empIdStr, out var parsed) ? parsed : (long?)null;
            return (role, email, empId);
        }

        private bool IsManager(string role) =>
            string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(role, "Manager", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(role, "TakımLideri", StringComparison.OrdinalIgnoreCase);

        private bool IsAdmin(string role) => string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase);

        // Rol rütbesi (hiyerarşi): Uzman(1) < TakımLideri(2) < Manager(3) < Admin(4).
        private static int RoleRank(string role) =>
            string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) ? 4 :
            string.Equals(role, "Manager", StringComparison.OrdinalIgnoreCase) ? 3 :
            string.Equals(role, "TakımLideri", StringComparison.OrdinalIgnoreCase) ? 2 : 1;

        // Hiyerarşik üste görev atanamaz: atanan/2. sorumlu rütbesi, atayanın rütbesinden YÜKSEK olamaz.
        private async Task EnsureCanAssignAsync(long? assigneeId, long? secondaryId, string myRole)
        {
            int myRank = RoleRank(myRole);
            foreach (var id in new[] { assigneeId, secondaryId })
            {
                if (!id.HasValue) continue;
                var emp = await _employeeRepository.GetAll().AsNoTracking()
                    .Where(e => e.Id == id.Value).Select(e => new { e.AppRole, e.FirstName, e.LastName }).FirstOrDefaultAsync();
                if (emp == null) continue;
                if (RoleRank(emp.AppRole) > myRank)
                    throw new UserFriendlyException(
                        $"Hiyerarşik üstünüze ({emp.FirstName} {emp.LastName}) görev atayamazsınız. İhtiyaçlarınızı üst yöneticinize iletebilirsiniz.");
            }
        }

        // Tüm takımlarda geçerli yönetici (config hariç admin gibi): Admin veya Manager.
        private static bool IsCrossTeamManager(string role) =>
            string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(role, "Manager", StringComparison.OrdinalIgnoreCase);

        // Tüm takımları görebilir mi (kapsam uygulanmaz): admin-self VEYA Manager.
        private bool SeesAllTeams()
        {
            var role = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Role)?.Value ?? "Uzman";
            return IsAdminSelfContext() || string.Equals(role, "Manager", StringComparison.OrdinalIgnoreCase);
        }

        // Admin-self mi (Sistem Yöneticisi kendi kimliği)? Login-as ile başka kişiye geçmişse false → kapsam uygulanır.
        private bool IsAdminSelfContext()
        {
            var user = _httpContextAccessor.HttpContext?.User;
            var role = user?.FindFirst(ClaimTypes.Role)?.Value ?? "Uzman";
            if (!string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase)) return false;
            long? empId = long.TryParse(user?.FindFirst("EmployeeId")?.Value, out var e) ? e : (long?)null;
            long? ownId = long.TryParse(user?.FindFirst("AdminOwnEmployeeId")?.Value, out var o) ? o : (long?)null;
            // config-admin kendi kimliğinde VEYA AdminOwnEmployeeId olmayan (Google) admin → self (tümünü görür)
            return !empId.HasValue || !ownId.HasValue || empId == ownId;
        }

        // Mevcut kullanıcının takımı - istek başına bir kez sorgulanır (cache'lenir)
        private bool _teamIdLoaded;
        private long? _currentTeamId;
        private long? CurrentEmployeeTeamId(long? employeeId)
        {
            if (_teamIdLoaded) return _currentTeamId;
            _teamIdLoaded = true;
            if (employeeId.HasValue)
                _currentTeamId = _employeeRepository.GetAll()
                    .Where(e => e.Id == employeeId.Value)
                    .Select(e => e.TeamId)
                    .FirstOrDefault();
            return _currentTeamId;
        }

        // Yönetici (Admin/TakımLideri) bu göreve erişebiliyor mu: Admin her zaman, TakımLideri sadece kendi takımının görevine
        private bool IsManagerForTask(TaskItem task, (string Role, string Email, long? EmployeeId) ctx)
        {
            if (!IsManager(ctx.Role)) return false;
            if (IsCrossTeamManager(ctx.Role)) return true; // Admin/Manager → tüm takımlar
            var myTeamId = CurrentEmployeeTeamId(ctx.EmployeeId);
            return !task.TeamId.HasValue || task.TeamId == myTeamId;
        }

        // Düzenleme/durum/yorum yetkisi: yönetici (kendi takımı) VEYA görevin sahibi
        private bool CanEdit(TaskItem task, (string Role, string Email, long? EmployeeId) ctx) =>
            IsManagerForTask(task, ctx) ||
            (task.AssignedEmployeeId.HasValue && ctx.EmployeeId.HasValue &&
             task.AssignedEmployeeId.Value == ctx.EmployeeId.Value);

        // Silme: Yönetici (kendi takımı) her şeyi silebilir; Uzman yalnızca kendisine atanmış görevi silebilir
        private bool CanDelete(TaskItem task, (string Role, string Email, long? EmployeeId) ctx)
        {
            if (IsManagerForTask(task, ctx)) return true;
            return task.AssignedEmployeeId.HasValue
                && ctx.EmployeeId.HasValue
                && task.AssignedEmployeeId.Value == ctx.EmployeeId.Value;
        }

        private void EnsureCanModify(TaskItem task)
        {
            if (!CanEdit(task, CurrentContext()))
                throw new UserFriendlyException("Bu görev size atanmadığı için üzerinde işlem yapamazsınız.");
        }

        private async Task EnsureCanDeleteAsync(long id)
        {
            var task = await _taskRepository.GetAsync(id);
            if (!CanDelete(task, CurrentContext()))
                throw new UserFriendlyException("Bu görevi silme yetkiniz yok.");
        }

        public async Task<PagedResultDto<TaskItemDto>> GetAllAsync(GetTasksInput input)
        {
            var query = _taskRepository.GetAll()
                .Include(t => t.Project)
                .Include(t => t.AssignedEmployee)
                .Include(t => t.SecondaryEmployee)
                .Include(t => t.AssignedByEmployee)
                .Include(t => t.SubCategory).ThenInclude(sc => sc.Category)
                .Include(t => t.Team)
                .WhereIf(!string.IsNullOrWhiteSpace(input.Filter), t => t.Title.Contains(input.Filter))
                .WhereIf(input.ProjectId.HasValue, t => t.ProjectId == input.ProjectId.Value)
                .WhereIf(input.SubCategoryId.HasValue, t => t.SubCategoryId == input.SubCategoryId.Value)
                .WhereIf(input.CategoryId.HasValue, t => t.SubCategory != null && t.SubCategory.CategoryId == input.CategoryId.Value)
                .WhereIf(input.TeamId.HasValue, t => t.TeamId == input.TeamId.Value)
                .WhereIf(input.CompletedFrom.HasValue, t => t.CompletedDate >= input.CompletedFrom.Value)
                .WhereIf(input.CompletedTo.HasValue, t => t.CompletedDate <= input.CompletedTo.Value)
                .WhereIf(input.IsLate == true, t => t.DueDate.HasValue && t.CompletedDate.HasValue && t.CompletedDate.Value > t.DueDate.Value)
                .WhereIf(input.IsLate == false, t => !(t.DueDate.HasValue && t.CompletedDate.HasValue && t.CompletedDate.Value > t.DueDate.Value))
                .WhereIf(input.AssignedEmployeeId.HasValue, t => t.AssignedEmployeeId == input.AssignedEmployeeId.Value)
                .WhereIf(input.Status.HasValue, t => t.Status == input.Status.Value)
                .WhereIf(input.Priority.HasValue, t => t.Priority == input.Priority.Value)
                .WhereIf(input.ActivityType.HasValue, t => t.ActivityType == input.ActivityType.Value)
                .WhereIf(!string.IsNullOrWhiteSpace(input.GroupName), t => t.GroupName == input.GroupName);

            // GÖRÜNÜRLÜK (satır bazında): Admin-self (Sistem Yöneticisi) tümünü görür; non-admin VEYA login-as ile
            // başka kişi olarak işlem yapan admin → yalnız o kişinin TAKIMININ (takımsız/kendine atanan dahil) görevleri.
            var scopeCtx = CurrentContext();
            if (!SeesAllTeams() && scopeCtx.EmployeeId.HasValue)
            {
                var myTeamId = CurrentEmployeeTeamId(scopeCtx.EmployeeId);
                query = query.Where(t =>
                    t.TeamId == null ||
                    t.TeamId == myTeamId ||
                    t.AssignedEmployeeId == scopeCtx.EmployeeId.Value ||
                    t.SecondaryEmployeeId == scopeCtx.EmployeeId.Value);
            }

            var count = await query.CountAsync();
            // V4: önem derecesine göre büyükten küçüğe (10 en üstte), sonra en yeni
            var items = await query.OrderByDescending(t => t.PriorityScore).ThenByDescending(t => t.CreationTime)
                .PageBy(input).ToListAsync();

            return new PagedResultDto<TaskItemDto>(count, items.Select(MapToDto).ToList());
        }

        public async Task<TaskItemDto> GetAsync(long id)
        {
            var task = await _taskRepository.GetAll()
                .Include(t => t.Project)
                .Include(t => t.AssignedEmployee)
                .Include(t => t.SecondaryEmployee)
                .Include(t => t.AssignedByEmployee)
                .Include(t => t.SubCategory).ThenInclude(sc => sc.Category)
                .Include(t => t.Comments)
                .Include(t => t.Attachments)
                .FirstOrDefaultAsync(t => t.Id == id);
            return MapToDto(task);
        }

        // Görev = ana kategori + alt kategori + atanan kişi. Yönetici herkese atar; Uzman yalnızca kendine.
        public async Task<TaskItemDto> CreateAsync(CreateUpdateTaskItemDto input)
        {
            var ctx = CurrentContext();

            // V4: Süre (tahmini saat) zorunluluğu kaldırıldı — 0/boş kabul edilir (takvimde tarih olması yeterli).
            // Önem derecesi 1-10 aralığına sabitlenir; renk uyumu için enum karşılığı da atanır.
            if (input.PriorityScore < 1) input.PriorityScore = 5;
            if (input.PriorityScore > 10) input.PriorityScore = 10;
            input.Priority = PriorityFromScore(input.PriorityScore);

            // Atayan Kişi (AssignedBy) otomasyonu:
            //  - Uzman/kullanıcı kendine görev açıyorsa: atanan ve atayan = kendisi
            //  - Takım Lideri/Admin atıyorsa: atayan alanı boşsa işlemi yapan yönetici seçilir
            if (!IsManager(ctx.Role))
            {
                input.AssignedEmployeeId = ctx.EmployeeId;
                input.AssignedByEmployeeId = ctx.EmployeeId;
            }
            else if (!input.AssignedByEmployeeId.HasValue)
            {
                input.AssignedByEmployeeId = ctx.EmployeeId;
            }

            // Proje görevi: kategori projeden kilitli miras (override), sorumlular ve SLA girilmemişse projeden dolar
            if (input.ProjectId.HasValue)
            {
                var proj = await _projectRepository.GetAll()
                    .Where(p => p.Id == input.ProjectId.Value)
                    .Select(p => new { p.PrimaryResponsibleId, p.SecondaryResponsibleId, p.SlaTargetDate, p.PlannedEndDate, p.SubCategoryId })
                    .FirstOrDefaultAsync();
                if (proj != null)
                {
                    // Kategori projeden kilitli (auto-fill + lock) — proje bir alt kategoriye bağlıysa görev de o kategoriye girer
                    if (proj.SubCategoryId.HasValue) input.SubCategoryId = proj.SubCategoryId;
                    if (!input.AssignedEmployeeId.HasValue) input.AssignedEmployeeId = proj.PrimaryResponsibleId;
                    if (!input.SecondaryEmployeeId.HasValue) input.SecondaryEmployeeId = proj.SecondaryResponsibleId;
                    // Son tarih girilmemişse projenin SLA (yoksa planlanan bitiş) tarihini ata → "son tarihi olmayan görev" olmaz
                    if (!input.DueDate.HasValue) input.DueDate = proj.SlaTargetDate ?? proj.PlannedEndDate;
                }
            }

            // Alt kategori seçildiyse geçerliliğini doğrula (herkes herhangi bir kategoriye görev ekleyebilir).
            if (input.SubCategoryId.HasValue)
            {
                var exists = await _subCategoryRepository.GetAll()
                    .AnyAsync(sc => sc.Id == input.SubCategoryId.Value);
                if (!exists)
                    throw new UserFriendlyException("Seçilen alt kategori bulunamadı.");
            }

            // Zaman çizelgesi SLA'ya göre listelendiğinden başlangıç ve bitiş tarihleri ZORUNLU.
            if (!input.StartDate.HasValue)
                throw new UserFriendlyException("Görev başlangıç tarihi zorunludur.");
            if (!input.DueDate.HasValue)
                throw new UserFriendlyException("Görev bitiş (son teslim) tarihi zorunludur. Proje görevlerinde proje SLA tarihi girilmelidir.");

            // İZİN kontrolü: atanacak 1. sorumlu izinliyse görev 2. sorumluya (yedek) atanır, izinli kişi yedeğe geçer.
            string assignmentNote = null;
            if (input.AssignedEmployeeId.HasValue)
            {
                var primaryEmp = await _employeeRepository.GetAll().AsNoTracking()
                    .FirstOrDefaultAsync(e => e.Id == input.AssignedEmployeeId.Value);
                if (IsOnLeaveNow(primaryEmp))
                {
                    if (input.SecondaryEmployeeId.HasValue && input.SecondaryEmployeeId.Value != input.AssignedEmployeeId.Value)
                    {
                        var secEmp = await _employeeRepository.GetAll().AsNoTracking()
                            .FirstOrDefaultAsync(e => e.Id == input.SecondaryEmployeeId.Value);
                        var onLeaveId = input.AssignedEmployeeId.Value;
                        input.AssignedEmployeeId = input.SecondaryEmployeeId;   // 2. sorumluya ata
                        input.SecondaryEmployeeId = onLeaveId;                  // izinli kişi yedeğe geçer
                        assignmentNote = $"1. sorumlu {primaryEmp.FullName} izinli olduğu için görev 2. sorumlu {secEmp?.FullName} kişisine atandı.";
                    }
                    else
                    {
                        assignmentNote = $"Uyarı: 1. sorumlu {primaryEmp.FullName} izinli ancak tanımlı bir 2. sorumlu (yedek) yok; görev yine de bu kişiye atandı.";
                    }
                }
            }

            // Hiyerarşi: kendinden yüksek role (üst yöneticiye) görev atanamaz.
            await EnsureCanAssignAsync(input.AssignedEmployeeId, input.SecondaryEmployeeId, ctx.Role);

            // Görev grubu boşsa: atanan kişinin BİRİMİ (Department) otomatik görev grubu olur.
            // (Birim değerleri görev grubuyla aynı standarttadır: "Sistem Birimi" / "Network Birimi".)
            if (string.IsNullOrWhiteSpace(input.GroupName) && input.AssignedEmployeeId.HasValue)
            {
                input.GroupName = await _employeeRepository.GetAll().AsNoTracking()
                    .Where(e => e.Id == input.AssignedEmployeeId.Value).Select(e => e.Department).FirstOrDefaultAsync();
            }

            var task = ObjectMapper.Map<TaskItem>(input);
            task.TenantId = AbpSession.TenantId ?? 1;
            task.TeamId = await ResolveTeamIdForNewTaskAsync(input, ctx);
            // Öz görev onay mekanizması: Uzman'ın açtığı görev Beklemede, yöneticininki Onaylandi
            task.ApprovalStatus = IsManager(ctx.Role)
                ? Entities.TaskApprovalStatus.Onaylandi
                : Entities.TaskApprovalStatus.Beklemede;
            // İlerleme yüzdesi duruma göre (Planlandı %0, Devam %25, Tamamlandı %100...)
            task.CompletionPercentage = ProgressForStatus(task.Status, input.CompletionPercentage);
            if (task.Status == Entities.TaskStatus.Tamamlandi && !task.CompletedDate.HasValue)
                task.CompletedDate = DateTime.Now;
            await _taskRepository.InsertAsync(task);
            await CurrentUnitOfWork.SaveChangesAsync();
            await NotifyTaskCreatedAsync(task);   // e-posta bildirimi (SMTP yoksa no-op)
            var createdDto = MapToDto(task);
            createdDto.AssignmentNote = assignmentNote; // izin nedeniyle yeniden atama bilgisi (varsa)
            return createdDto;
        }

        // Görev oluşunca: atanan kişiye "görev atandı"; onay bekliyorsa takım liderine "onay bekliyor".
        private async Task NotifyTaskCreatedAsync(TaskItem task)
        {
            try
            {
                if (task.AssignedEmployeeId.HasValue)
                {
                    // In-app bildirim (kendine değilse)
                    await _notificationManager.NotifyAsync(task.AssignedEmployeeId, Entities.NotificationType.GorevAtandi,
                        "Size bir görev atandı", task.Title, $"/Tasks/Detail/{task.Id}", severity: "info",
                        actorEmployeeId: CurrentContext().EmployeeId);

                    var emp = await _employeeRepository.GetAll().AsNoTracking()
                        .FirstOrDefaultAsync(e => e.Id == task.AssignedEmployeeId.Value);
                    if (emp != null && !string.IsNullOrWhiteSpace(emp.Email))
                    {
                        var due = task.DueDate?.ToString("dd.MM.yyyy") ?? "-";
                        await _emailSender.SendAsync(emp.Email,
                            $"Yeni görev atandı: {task.Title}",
                            $"<p>Merhaba {emp.FullName},</p><p><b>{System.Net.WebUtility.HtmlEncode(task.Title)}</b> görevi size atandı.</p><p>Son tarih: <b>{due}</b> · Önem: <b>{task.PriorityScore}/10</b></p>");
                    }
                }
                if (task.ApprovalStatus == Entities.TaskApprovalStatus.Beklemede && task.TeamId.HasValue)
                {
                    var leaderId = await _teamRepository.GetAll().AsNoTracking()
                        .Where(t => t.Id == task.TeamId.Value).Select(t => t.LeaderId).FirstOrDefaultAsync();
                    if (leaderId.HasValue)
                    {
                        var leader = await _employeeRepository.GetAll().AsNoTracking()
                            .FirstOrDefaultAsync(e => e.Id == leaderId.Value);
                        if (leader != null && !string.IsNullOrWhiteSpace(leader.Email))
                            await _emailSender.SendAsync(leader.Email,
                                $"Onay bekleyen görev: {task.Title}",
                                $"<p>Merhaba {leader.FullName},</p><p><b>{System.Net.WebUtility.HtmlEncode(task.Title)}</b> görevi onayınızı bekliyor.</p>");
                    }
                }
            }
            catch { /* bildirim ana akışı bozmaz */ }
        }

        // Yeni görevin takımı: projeden, o da yoksa oluşturan kişinin takımından miras alınır.
        private async Task<long?> ResolveTeamIdForNewTaskAsync(CreateUpdateTaskItemDto input, (string Role, string Email, long? EmployeeId) ctx)
        {
            if (input.ProjectId.HasValue)
            {
                var projectTeamId = await _projectRepository.GetAll()
                    .Where(p => p.Id == input.ProjectId.Value)
                    .Select(p => p.TeamId)
                    .FirstOrDefaultAsync();
                if (projectTeamId.HasValue) return projectTeamId;
            }

            if (ctx.EmployeeId.HasValue)
            {
                return await _employeeRepository.GetAll()
                    .Where(e => e.Id == ctx.EmployeeId.Value)
                    .Select(e => e.TeamId)
                    .FirstOrDefaultAsync();
            }

            return null;
        }

        public async Task<TaskItemDto> UpdateAsync(CreateUpdateTaskItemDto input)
        {
            var task = await _taskRepository.GetAsync(input.Id);
            EnsureCanModify(task);

            // Önem derecesi 1-10 clamp + renk enum senkronu
            if (input.PriorityScore < 1) input.PriorityScore = 5;
            if (input.PriorityScore > 10) input.PriorityScore = 10;
            input.Priority = PriorityFromScore(input.PriorityScore);

            // Başlangıç ve bitiş tarihleri zorunlu (zaman çizelgesi/SLA için)
            if (!input.StartDate.HasValue)
                throw new UserFriendlyException("Görev başlangıç tarihi zorunludur.");
            if (!input.DueDate.HasValue)
                throw new UserFriendlyException("Görev bitiş (son teslim) tarihi zorunludur.");

            // Uzman; atama, proje ve atayan bilgilerini değiştiremesin (sadece yönetici)
            var ctx = CurrentContext();
            if (!IsManager(ctx.Role))
            {
                input.AssignedEmployeeId = task.AssignedEmployeeId;
                input.SecondaryEmployeeId = task.SecondaryEmployeeId;
                input.AssignedByEmployeeId = task.AssignedByEmployeeId;
                input.ProjectId = task.ProjectId;
            }

            // Boş görev "Tamamlandı" yapılamaz — Tamamlandı'ya GEÇERKEN efor zorunlu.
            if (input.Status == Entities.TaskStatus.Tamamlandi && task.Status != Entities.TaskStatus.Tamamlandi)
                await EnsureHasEffortForCompletionAsync(task.Id);

            // Hiyerarşi: kendinden yüksek role (üst yöneticiye) görev atanamaz.
            await EnsureCanAssignAsync(input.AssignedEmployeeId, input.SecondaryEmployeeId, ctx.Role);

            var prevAssignee = task.AssignedEmployeeId;
            ObjectMapper.Map(input, task);
            // İlerleme yüzdesi duruma göre kurgulanır (Tamamlandı %100, Planlandı %0, Devam taban %25 / elle değer korunur)
            task.CompletionPercentage = ProgressForStatus(task.Status, input.CompletionPercentage);
            // Tamamlandı VEYA Kapatıldı (arşiv) → tamamlanma tarihi korunur (rapor/geçmiş için); diğer durumlarda sıfırlanır.
            task.CompletedDate = (task.Status == Entities.TaskStatus.Tamamlandi || task.Status == Entities.TaskStatus.Kapatildi)
                ? (task.CompletedDate ?? DateTime.Now)
                : (DateTime?)null;
            await CurrentUnitOfWork.SaveChangesAsync();

            // Yeniden atama bildirimi: yeni bir kişiye atandıysa (kendine değilse)
            if (task.AssignedEmployeeId.HasValue && task.AssignedEmployeeId != prevAssignee)
                await _notificationManager.NotifyAsync(task.AssignedEmployeeId, Entities.NotificationType.GorevAtandi,
                    "Size bir görev atandı", task.Title, $"/Tasks/Detail/{task.Id}", severity: "info", actorEmployeeId: ctx.EmployeeId);

            return MapToDto(task);
        }

        public async Task DeleteAsync(long id)
        {
            await EnsureCanDeleteAsync(id);
            await _taskRepository.DeleteAsync(id);
        }

        // Yorum/not silme: görevin sahibi (atanan) veya yöneticisi silebilir.
        public async Task DeleteCommentAsync(long commentId)
        {
            var ctx = CurrentContext();
            var c = await _commentRepository.FirstOrDefaultAsync(commentId);
            if (c == null) return;
            var task = await _taskRepository.FirstOrDefaultAsync(c.TaskItemId);
            bool can = task != null && (IsManagerForTask(task, ctx) || CanEdit(task, ctx));
            if (!can) throw new UserFriendlyException("Bu yorumu silme yetkiniz yok.");
            await _commentRepository.DeleteAsync(commentId);
        }

        // Göreve efor girişi (Görev Detay ekranı). Giriş yapan kişi adına; ActualHours senkronlanır.
        public async Task<long> LogEffortAsync(ActivityManagement.Activities.Dto.CreateActivityLogDto input)
        {
            var ctx = CurrentContext();
            if (!ctx.EmployeeId.HasValue)
                throw new UserFriendlyException("Efor girişi için personel kaydınız bulunmuyor.");
            if (input == null || !input.TaskItemId.HasValue)
                throw new UserFriendlyException("Efor girişi için görev gereklidir.");
            if (input.HoursSpent <= 0)
                throw new UserFriendlyException("Harcanan süre 0'dan büyük olmalıdır.");

            var task = await _taskRepository.GetAll().AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == input.TaskItemId.Value);
            if (task == null) throw new UserFriendlyException("Görev bulunamadı.");

            // Efor yalnız görevin atananı / 2. sorumlusu VEYA görevi yönetebilen (Admin/Lider) tarafından girilir.
            bool can = (task.AssignedEmployeeId.HasValue && task.AssignedEmployeeId == ctx.EmployeeId)
                       || (task.SecondaryEmployeeId.HasValue && task.SecondaryEmployeeId == ctx.EmployeeId)
                       || IsManagerForTask(task, ctx);
            if (!can) throw new UserFriendlyException("Bu göreve efor girme yetkiniz yok.");

            await _activityLogRepository.InsertAsync(new ActivityLog
            {
                TenantId = AbpSession.TenantId ?? 1,
                EmployeeId = ctx.EmployeeId.Value,      // 0/istemci değeri yok sayılır; her zaman giriş yapan kişi
                TaskItemId = task.Id,
                ProjectId = task.ProjectId,             // görev projeye bağlıysa efor projeye de sayılır (raporlama)
                Description = input.Description,
                ActivityDate = input.ActivityDate == default ? DateTime.Today : input.ActivityDate,
                HoursSpent = input.HoursSpent,
                ActivityType = string.IsNullOrWhiteSpace(input.ActivityType) ? "Görev" : input.ActivityType
            });
            await CurrentUnitOfWork.SaveChangesAsync();

            var sum = await _activityLogRepository.GetAll()
                .Where(l => l.TaskItemId == task.Id).Select(l => (decimal?)l.HoursSpent).SumAsync() ?? 0m;
            var trk = await _taskRepository.GetAsync(task.Id);
            if (trk.ActualHours != sum) { trk.ActualHours = sum; await CurrentUnitOfWork.SaveChangesAsync(); }
            return task.Id;
        }

        // Duruma göre ilerleme yüzdesi kurgusu:
        //  Beklemede/Planlandı → %0, DevamEdiyor → taban %25 (kullanıcı 30/40.. yapabilir; girilmişse korunur),
        //  Ertelendi → mevcut korunur, İptal → mevcut korunur, Tamamlandı → %100.
        private static int ProgressForStatus(Entities.TaskStatus status, int currentPct)
        {
            switch (status)
            {
                case Entities.TaskStatus.Tamamlandi:
                case Entities.TaskStatus.Kapatildi: return 100; // Kapatıldı = tamamlanmış (arşiv)
                case Entities.TaskStatus.Beklemede: return 0;
                case Entities.TaskStatus.DevamEdiyor:
                    return (currentPct <= 0 || currentPct >= 100) ? 25 : currentPct; // taban 25, elle girilen ara değer korunur
                default: // Ertelendi / İptal
                    return currentPct < 0 ? 0 : (currentPct > 100 ? 100 : currentPct);
            }
        }

        // Görev durumu → Türkçe etiket (tüm @t.StatusText kullanan view'lar buradan beslenir).
        private static string StatusText(Entities.TaskStatus s)
        {
            switch (s)
            {
                case Entities.TaskStatus.Beklemede: return "Beklemede";
                case Entities.TaskStatus.DevamEdiyor: return "Devam Ediyor";
                case Entities.TaskStatus.Tamamlandi: return "Tamamlandı";
                case Entities.TaskStatus.Iptal: return "İptal";
                case Entities.TaskStatus.Ertelendi: return "Ertelendi";
                case Entities.TaskStatus.Kapatildi: return "Kapatıldı";
                default: return s.ToString();
            }
        }

        public async Task UpdateStatusAsync(long id, Entities.TaskStatus status, int percentage)
        {
            var task = await _taskRepository.GetAsync(id);
            EnsureCanModify(task);
            // Boş görev "Tamamlandı" yapılamaz — Tamamlandı'ya GEÇERKEN efor zorunlu.
            if (status == Entities.TaskStatus.Tamamlandi && task.Status != Entities.TaskStatus.Tamamlandi)
                await EnsureHasEffortForCompletionAsync(id);
            task.Status = status;
            // Panodan/istemciden gelen yüzde varsa onu baz al, yoksa mevcut; duruma göre kurgu uygula
            task.CompletionPercentage = ProgressForStatus(status, percentage > 0 ? percentage : task.CompletionPercentage);
            // Tamamlandı/Kapatıldı → tamamlanma tarihi korunur; diğerlerinde sıfırlanır.
            task.CompletedDate = (status == Entities.TaskStatus.Tamamlandi || status == Entities.TaskStatus.Kapatildi)
                ? (task.CompletedDate ?? DateTime.Now) : (DateTime?)null;
        }

        public async Task<long> AddCommentAsync(long taskId, string comment, bool isInternal = false)
        {
            var task = await _taskRepository.GetAsync(taskId);
            EnsureCanModify(task);
            var ctx = CurrentContext();
            // Dahili not yalnızca yönetici ekleyebilir
            if (isInternal && !IsManager(ctx.Role)) isInternal = false;
            var author = ctx.EmployeeId.HasValue
                ? _employeeRepository.Get(ctx.EmployeeId.Value).FullName
                : (ctx.Email ?? "Bilinmiyor");
            var entity = new TaskComment
            {
                TaskItemId = taskId,
                Comment = comment,
                AuthorName = author,
                IsInternal = isInternal,
                TenantId = AbpSession.TenantId ?? 1
            };
            await _commentRepository.InsertAsync(entity);
            await CurrentUnitOfWork.SaveChangesAsync();
            return entity.Id;
        }

        // Görev onaylama/reddetme (yalnızca yönetici — kendi takımı)
        public async Task SetApprovalAsync(long id, Entities.TaskApprovalStatus status)
        {
            var task = await _taskRepository.GetAsync(id);
            var ctx = CurrentContext();
            if (!IsManagerForTask(task, ctx))
                throw new UserFriendlyException("Görev onay durumunu yalnızca yetkili yönetici değiştirebilir.");
            task.ApprovalStatus = status;
            await CurrentUnitOfWork.SaveChangesAsync();
        }

        // Yorum/göreve dosya eki kaydı (dosya diske Web katmanında yazılır, burada meta kaydedilir)
        public async Task<long> AddAttachmentAsync(long taskId, long? taskCommentId, string fileName, string filePath, long fileSize, string contentType)
        {
            var task = await _taskRepository.GetAsync(taskId);
            EnsureCanModify(task);
            var att = new TaskAttachment
            {
                TenantId = AbpSession.TenantId ?? 1,
                TaskItemId = taskId,
                TaskCommentId = taskCommentId,
                FileName = fileName,
                FilePath = filePath,
                FileSize = fileSize,
                ContentType = contentType
            };
            await _attachmentRepository.InsertAsync(att);
            await CurrentUnitOfWork.SaveChangesAsync();
            return att.Id;
        }

        // "Görevlerim" ekranı: kendisine 1. veya 2. sorumlu olarak atanan görevler,
        // önem derecesi yüksek olanlar üstte
        public async Task<ListResultDto<TaskItemDto>> GetEmployeeTasksAsync(long employeeId)
        {
            var tasks = await _taskRepository.GetAll()
                .Include(t => t.Project)
                .Include(t => t.AssignedEmployee)
                .Include(t => t.SecondaryEmployee)
                .Include(t => t.SubCategory).ThenInclude(sc => sc.Category)
                .Where(t => t.AssignedEmployeeId == employeeId || t.SecondaryEmployeeId == employeeId)
                .OrderByDescending(t => t.PriorityScore)   // V4: önem derecesi (10 en üstte)
                .ThenBy(t => t.DueDate)
                .ToListAsync();
            return new ListResultDto<TaskItemDto>(tasks.Select(MapToDto).ToList());
        }

        public async Task<ListResultDto<TaskItemDto>> GetCalendarTasksAsync(long employeeId, int year, int month)
        {
            var startDate = new DateTime(year, month, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);

            // Görev, başlangıç VEYA son tarihi bu ay içindeyse takvimde görünür (başlangıç saatiyle çizilir).
            var tasks = await _taskRepository.GetAll()
                .Include(t => t.Project)
                .Where(t => t.AssignedEmployeeId == employeeId &&
                            ((t.DueDate >= startDate && t.DueDate <= endDate) ||
                             (t.StartDate >= startDate && t.StartDate <= endDate)))
                .ToListAsync();
            return new ListResultDto<TaskItemDto>(tasks.Select(MapToDto).ToList());
        }

        private static readonly string[] ActivityTypeLabels = new[]
        {
            "Bakım","Geliştirme","Kurulum","Destek","Test","Dokümantasyon","Eğitim","Analiz","Proje","Diğer"
        };

        // Önem derecesi (1-10) → renk için TaskPriority enum karşılığı
        private static TaskPriority PriorityFromScore(int score) =>
            score >= 9 ? TaskPriority.Kritik :
            score >= 6 ? TaskPriority.Yuksek :
            score >= 4 ? TaskPriority.Normal :
                         TaskPriority.Dusuk;

        private static string ApprovalText(Entities.TaskApprovalStatus s) => s switch
        {
            Entities.TaskApprovalStatus.Beklemede => "Onay Bekliyor",
            Entities.TaskApprovalStatus.Onaylandi => "Onaylandı",
            Entities.TaskApprovalStatus.Reddedildi => "Reddedildi",
            _ => s.ToString()
        };

        private static TaskAttachmentDto MapAttachment(TaskAttachment a) => new TaskAttachmentDto
        {
            Id = a.Id, FileName = a.FileName, FilePath = a.FilePath,
            FileSize = a.FileSize, ContentType = a.ContentType
        };

        private TaskItemDto MapToDto(TaskItem t)
        {
            if (t == null) return null;
            var dto = ObjectMapper.Map<TaskItemDto>(t);
            dto.ProjectName = t.Project?.Name;
            dto.AssignedEmployeeName = t.AssignedEmployee?.FullName;
            dto.SecondaryEmployeeName = t.SecondaryEmployee?.FullName;
            dto.AssignedByEmployeeName = t.AssignedByEmployee?.FullName;
            dto.StatusText = StatusText(t.Status);
            dto.PriorityText = t.Priority.ToString();
            dto.ApprovalStatusText = ApprovalText(t.ApprovalStatus);
            dto.SubCategoryName = t.SubCategory?.Name;
            dto.CategoryId = t.SubCategory?.CategoryId;
            dto.CategoryName = t.SubCategory?.Category?.Name;
            dto.TeamName = t.Team?.Name;
            dto.CompletedOnTime = (t.DueDate.HasValue && t.CompletedDate.HasValue)
                ? t.CompletedDate.Value <= t.DueDate.Value
                : (bool?)null;
            var atIdx = t.ActivityType.HasValue ? (int)t.ActivityType.Value : -1;
            dto.ActivityTypeText = (atIdx >= 0 && atIdx < ActivityTypeLabels.Length)
                ? ActivityTypeLabels[atIdx]
                : null;
            var ctx = CurrentContext();
            dto.CanEdit = CanEdit(t, ctx);
            dto.CanDelete = CanDelete(t, ctx);
            dto.CanApprove = IsManagerForTask(t, ctx) && t.ApprovalStatus == Entities.TaskApprovalStatus.Beklemede;

            bool isMgr = IsManager(ctx.Role);
            var atts = t.Attachments ?? new List<TaskAttachment>();
            dto.Comments = (t.Comments ?? new List<TaskComment>())
                .Where(c => isMgr || !c.IsInternal) // dahili notları yalnızca yönetici görür
                .OrderBy(c => c.CreationTime)
                .Select(c => new TaskCommentDto
                {
                    Id = c.Id,
                    Comment = c.Comment,
                    AuthorName = c.AuthorName,
                    IsInternal = c.IsInternal,
                    CreationTime = c.CreationTime,
                    Attachments = atts.Where(a => a.TaskCommentId == c.Id).Select(MapAttachment).ToList()
                }).ToList();
            // Yoruma bağlı olmayan (doğrudan göreve eklenen) ekler
            dto.Attachments = atts.Where(a => !a.TaskCommentId.HasValue).Select(MapAttachment).ToList();
            return dto;
        }
    }
}
