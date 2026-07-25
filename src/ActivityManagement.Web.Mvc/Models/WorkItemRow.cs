using System;

namespace ActivityManagement.Web.Models
{
    // "İşlerim → Genel Bakış" için Görev / Talep / Faaliyet'i tek listede birleştiren satır.
    public class WorkItemRow
    {
        public string Kind { get; set; }        // "Görev" | "Talep" | "Faaliyet"
        public string KindIcon { get; set; }     // fontawesome ikon sınıfı
        public string KindColor { get; set; }    // rozet rengi (bootstrap)
        public long Id { get; set; }
        public string Title { get; set; }
        public string Link { get; set; }
        public string StatusText { get; set; }
        public string StatusColor { get; set; }
        public string Context { get; set; }      // proje/kategori/kaynak bilgisi
        public DateTime? DueDate { get; set; }
        public int PriorityScore { get; set; }
        public int Percentage { get; set; }
        public bool IsOverdue { get; set; }

        // Sıralama anahtarı: gecikmiş önce, sonra yakın SLA, sonra yüksek önem.
        public DateTime SortDue => DueDate ?? DateTime.MaxValue;
    }
}
