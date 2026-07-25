using System;
using System.Collections.Generic;
using Abp.Application.Services.Dto;
using ActivityManagement.Entities;

namespace ActivityManagement.Notifications.Dto
{
    public class NotificationDto : EntityDto<long>
    {
        public NotificationType Type { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public string Link { get; set; }
        public string Icon { get; set; }
        public string Severity { get; set; }
        public bool IsRead { get; set; }
        public DateTime CreationTime { get; set; }
        public string TimeAgo { get; set; }   // "5 dk önce" gibi
    }

    // Zil ikonu + açılır liste + polling toast için özet.
    public class NotificationSummaryDto
    {
        public int UnreadCount { get; set; }
        public List<NotificationDto> Recent { get; set; } = new List<NotificationDto>();
    }
}
