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

    // İstek/mesaj gönderilebilecek üst yönetici.
    public class MessageRecipientDto
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public string RoleLabel { get; set; }
    }

    // Bildirim tercihleri (kullanıcı ekranı).
    public class NotificationPreferenceDto
    {
        public bool HasEmployee { get; set; }          // personel kaydı yoksa tercih kaydedilemez
        public bool EmailEnabled { get; set; } = true;
        public List<NotificationTypePrefDto> Types { get; set; } = new List<NotificationTypePrefDto>();
    }

    public class NotificationTypePrefDto
    {
        public int Type { get; set; }
        public string Label { get; set; }
        public bool InAppEnabled { get; set; }   // true = açık (susturulmamış)
    }

    public class SaveNotificationPreferenceInput
    {
        public bool EmailEnabled { get; set; }
        // in-app'te AÇIK bırakılan tipler (checkbox işaretli olanlar). Muted = tüm tipler − bunlar.
        public List<int> EnabledInAppTypes { get; set; } = new List<int>();
    }
}
