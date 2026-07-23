using System;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace ActivityManagement.Security
{
    // appsettings.json içindeki sırları (connection string şifresi, OAuth client secret, admin şifresi)
    // cleartext tutmamak için Windows DPAPI (LocalMachine scope) ile şifreler/çözer.
    // MD5 tek yönlüdür (geri açılamaz) ve Base64 hiç şifreleme değildir - ikisi de bu amaç için uygun değil.
    // DPAPI, bu makineye (ve bu makinede çalışan herhangi bir işleme) özel, gerçekten geri çözülebilir bir şifrelemedir.
    [SupportedOSPlatform("windows")]
    public static class DpapiProtector
    {
        private const string Prefix = "DPAPI:";

        public static string Protect(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return plainText;
            var bytes = Encoding.UTF8.GetBytes(plainText);
            var encrypted = ProtectedData.Protect(bytes, null, DataProtectionScope.LocalMachine);
            return Prefix + Convert.ToBase64String(encrypted);
        }

        // "DPAPI:" öneki yoksa değeri olduğu gibi döndürür (geriye dönük uyumluluk / henüz şifrelenmemiş değerler için)
        public static string Unprotect(string value)
        {
            if (string.IsNullOrEmpty(value) || !value.StartsWith(Prefix, StringComparison.Ordinal))
                return value;

            var encrypted = Convert.FromBase64String(value.Substring(Prefix.Length));
            var bytes = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.LocalMachine);
            return Encoding.UTF8.GetString(bytes);
        }
    }
}
