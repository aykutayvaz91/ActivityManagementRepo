using System.Threading;

namespace ActivityManagement.Auditing
{
    // İstek başına giriş yapan kullanıcı bilgisini DbContext'e taşımak için ambient (AsyncLocal) bağlam.
    // Middleware her istekte doldurur; DbContext SaveChanges audit yazarken okur.
    public static class AuditUserContext
    {
        private static readonly AsyncLocal<AuditUser> _current = new AsyncLocal<AuditUser>();

        public static AuditUser Current
        {
            get => _current.Value;
            set => _current.Value = value;
        }
    }

    public class AuditUser
    {
        public long? UserId { get; set; }
        public string UserName { get; set; }
        public string Ip { get; set; }
    }
}
