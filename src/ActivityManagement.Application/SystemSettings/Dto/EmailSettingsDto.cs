namespace ActivityManagement.SystemSettings.Dto
{
    public class EmailSettingsDto
    {
        public long Id { get; set; }
        public string SenderEmail { get; set; }
        public string SenderDisplayName { get; set; }
        public string SmtpHost { get; set; }
        public int SmtpPort { get; set; } = 587;
        public string SmtpUserName { get; set; }
        // Şifre asla istemciye geri gönderilmez
        public bool HasPassword { get; set; }
        public bool EnableSsl { get; set; } = true;
    }

    public class UpdateEmailSettingsDto
    {
        public string SenderEmail { get; set; }
        public string SenderDisplayName { get; set; }
        public string SmtpHost { get; set; }
        public int SmtpPort { get; set; } = 587;
        public string SmtpUserName { get; set; }
        // Boş bırakılırsa mevcut şifre korunur
        public string SmtpPassword { get; set; }
        public bool EnableSsl { get; set; } = true;
    }
}
