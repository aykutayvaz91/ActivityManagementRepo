using System.Collections.Generic;
using System.Threading.Tasks;
using Abp.Application.Services;
using ActivityManagement.Activities.Dto;

namespace ActivityManagement.Activities
{
    public interface IActivitySubjectAppService : IApplicationService
    {
        Task<List<ActivitySubjectDto>> GetAllAsync(GetActivitySubjectsInput input);
        Task<List<ActivitySubjectDto>> GetByProjectAsync(long projectId);
        Task<ActivitySubjectDto> GetAsync(long id);
        Task<ActivitySubjectDto> CreateAsync(CreateUpdateActivitySubjectDto input);
        Task<ActivitySubjectDto> UpdateAsync(CreateUpdateActivitySubjectDto input);
        Task DeleteAsync(long id);

        // Uzman/yönetici bir faaliyet konusuna efor (ActivityLog) girer
        Task<ActivityLogDto> LogEffortAsync(CreateActivityLogDto input);
        Task<List<ActivityLogDto>> GetEffortsAsync(long activitySubjectId);
        Task DeleteEffortAsync(long id);
        // V4: günü 8 saate tamamla (1. sorumlu sistemler için 1'er saat rutin kontrol)
        Task<int> CompleteDayTo8HoursAsync(System.DateTime? date = null);
        // R1: günlük efor özeti + serbest ekleme + düzenleme
        Task<DayEffortDto> GetDayEffortsAsync(System.DateTime? date = null);
        Task AddManualEffortAsync(System.DateTime date, decimal hoursSpent, string description, string activityType, long? taskItemId = null, long? projectId = null, long? serviceRequestId = null);
        Task UpdateEffortAsync(long id, decimal hoursSpent, string description, System.DateTime activityDate, string activityType, long? projectId = null);
    }
}
