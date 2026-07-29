using System.Threading.Tasks;
using Abp.Application.Services;
using ActivityManagement.Search.Dto;

namespace ActivityManagement.Search
{
    public interface IGlobalSearchAppService : IApplicationService
    {
        // Görev + Talep + Faaliyet + Proje + Kişi genelinde arama (görünürlük kapsamına göre).
        Task<GlobalSearchResultDto> SearchAsync(string q, int perType = 8);
    }
}
