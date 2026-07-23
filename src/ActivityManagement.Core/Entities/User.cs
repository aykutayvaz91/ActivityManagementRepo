using System;
using Abp.Authorization.Users;

namespace ActivityManagement.Entities
{
    public class User : AbpUser<User>
    {
        public static string CreateRandomPassword()
        {
            return Guid.NewGuid().ToString("N").Substring(0, 16);
        }

        public override string ToString()
        {
            return $"[User {Id}] {FullName}";
        }
    }
}
