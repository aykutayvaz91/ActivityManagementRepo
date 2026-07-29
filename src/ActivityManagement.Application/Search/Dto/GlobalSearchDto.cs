using System.Collections.Generic;

namespace ActivityManagement.Search.Dto
{
    public class SearchHitDto
    {
        public string Type { get; set; }     // Görev / Talep / Faaliyet / Proje / Kişi
        public string Icon { get; set; }      // fontawesome sınıfı
        public long Id { get; set; }
        public string Title { get; set; }
        public string Subtitle { get; set; }
        public string Url { get; set; }
    }

    public class GlobalSearchResultDto
    {
        public string Query { get; set; }
        public List<SearchHitDto> Tasks { get; set; } = new();
        public List<SearchHitDto> Requests { get; set; } = new();
        public List<SearchHitDto> Activities { get; set; } = new();
        public List<SearchHitDto> Projects { get; set; } = new();
        public List<SearchHitDto> Employees { get; set; } = new();
        public int Total => Tasks.Count + Requests.Count + Activities.Count + Projects.Count + Employees.Count;
    }
}
