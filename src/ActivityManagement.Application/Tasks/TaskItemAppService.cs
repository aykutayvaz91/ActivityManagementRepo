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
        private readonly IHttpContextAccessor _httpContextAccessor;

        public TaskItemAppService(
            IRepository<TaskItem, long> taskRepository,
            IRepository<TaskComment, long> commentRepository,
            IRepository<TaskAttachment, long> attachmentRepository,
            IRepository<Employee, long> employeeRepository,
            IRepository<SubCategory, long> subCategoryRepository,
            IRepository<Project, long> projectRepository,
            IHttpContextAccessor httpContextAccessor)
        {
            _taskRepository = taskRepository;
            _commentRepository = commentRepository;
            _attachmentRepository = attachmentRepository;
            _employeeRepository = employeeRepository;
            _subCategoryRepository = subCategoryRepository;
            _projectRepository = projectRepository;
            _httpContextAccessor = httpContextAccessor;
        }

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
            string.Equals(role, "TakımLideri", StringComparison.OrdinalIgnoreCase);

        private bool IsAdmin(string role) => string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase);

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
            if (IsAdmin(ctx.Role)) return true;
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

            // GÖRÜNÜRLÜK (satır bazında): Admin tümünü görür; Admin olmayan yalnız kendi TAKIMININ
            // (takımsız/kendine atanan dahil) görevlerini görür. Pano/liste dahil tüm çağrılar için
            // sunucu tarafında zorlanır (controller filtresine güvenmez) — çok-takım sızmasını engeller.
            var scopeCtx = CurrentContext();
            if (!IsAdmin(scopeCtx.Role) && scopeCtx.EmployeeId.HasValue)
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
                if (primaryEmp != null && primaryEmp.IsOnLeave)
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

            var task = ObjectMapper.Map<TaskItem>(input);
            task.TenantId = AbpSession.TenantId ?? 1;
            task.TeamId = await ResolveTeamIdForNewTaskAsync(input, ctx);
            // Öz görev onay mekanizması: Uzman'ın açtığı görev Beklemede, yöneticininki Onaylandi
            task.ApprovalStatus = IsManager(ctx.Role)
                ? Entities.TaskApprovalStatus.Onaylandi
                : Entities.TaskApprovalStatus.Beklemede;
            await _taskRepository.InsertAsync(task);
            await CurrentUnitOfWork.SaveChangesAsync();
            var createdDto = MapToDto(task);
            createdDto.AssignmentNote = assignmentNote; // izin nedeniyle yeniden atama bilgisi (varsa)
            return createdDto;
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

            ObjectMapper.Map(input, task);
            await CurrentUnitOfWork.SaveChangesAsync();
            return MapToDto(task);
        }

        public async Task DeleteAsync(long id)
        {
            await EnsureCanDeleteAsync(id);
            await _taskRepository.DeleteAsync(id);
        }

        public async Task UpdateStatusAsync(long id, Entities.TaskStatus status, int percentage)
        {
            var task = await _taskRepository.GetAsync(id);
            EnsureCanModify(task);
            task.Status = status;
            task.CompletionPercentage = percentage;
            if (status == Entities.TaskStatus.Tamamlandi)
                task.CompletedDate = DateTime.Now;
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
            dto.StatusText = t.Status.ToString();
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
