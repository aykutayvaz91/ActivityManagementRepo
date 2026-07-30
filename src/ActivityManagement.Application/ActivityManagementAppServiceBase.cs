using System;
using System.Linq;
using System.Security.Claims;
using Abp.Application.Services;
using Abp.Domain.Repositories;
using Microsoft.AspNetCore.Http;
using ActivityManagement.Entities;

namespace ActivityManagement
{
    public abstract class ActivityManagementAppServiceBase : ApplicationService
    {
        protected ActivityManagementAppServiceBase()
        {
            LocalizationSourceName = ActivityManagementConsts.LocalizationSourceName;
        }

        // ABP/Windsor PROPERTY INJECTION — ortak yetki yardımcıları için (public setter → otomatik enjekte edilir).
        // NOT: Türeten servisler ayrıca kendi _httpContextAccessor/_employeeRepository alanlarını enjekte etmeye devam eder;
        // bunlar yalnız base'teki ortak helper'lar içindir.
        public IHttpContextAccessor AuthHttpContextAccessor { get; set; }
        public IRepository<Employee, long> AuthEmployeeRepository { get; set; }

        private ClaimsPrincipal AuthUser => AuthHttpContextAccessor?.HttpContext?.User;

        // İstek başına cache (ApplicationService transient → örnek başına = istek başına).
        private bool _empRoleLoaded;
        private string _currentEmpRole;
        private bool _teamIdLoaded;
        private long? _currentTeamId;

        // ---- Ortak yetki yardımcıları (ServiceRequest / TaskItem / ActivitySubject ile birebir aynı) ----

        protected (string Role, string Email, long? EmployeeId) CurrentContext()
        {
            var user = AuthUser;
            var role = user?.FindFirst(ClaimTypes.Role)?.Value ?? "Uzman";
            var email = user?.FindFirst(ClaimTypes.Email)?.Value ?? user?.FindFirst(ClaimTypes.Name)?.Value;
            var empIdStr = user?.FindFirst("EmployeeId")?.Value;
            long? empId = long.TryParse(empIdStr, out var parsed) ? parsed : (long?)null;
            return (role, email, empId);
        }

        protected bool IsManager(string role) =>
            string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(role, "Manager", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(role, "TakımLideri", StringComparison.OrdinalIgnoreCase);

        protected bool IsCrossTeamManager(string role) =>
            string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(role, "Manager", StringComparison.OrdinalIgnoreCase);

        protected bool IsAdmin(string role) =>
            string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase);

        // config-admin kendi bağlamında mı (login-as ile başka kişiye geçmemiş)
        protected bool IsAdminSelfContext()
        {
            var user = AuthUser;
            var role = user?.FindFirst(ClaimTypes.Role)?.Value ?? "Uzman";
            if (!string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase)) return false;
            long? empId = long.TryParse(user?.FindFirst("EmployeeId")?.Value, out var e) ? e : (long?)null;
            long? ownId = long.TryParse(user?.FindFirst("AdminOwnEmployeeId")?.Value, out var o) ? o : (long?)null;
            return !empId.HasValue || !ownId.HasValue || empId == ownId;
        }

        protected bool SeesAllTeams()
        {
            var user = AuthUser;
            var role = user?.FindFirst(ClaimTypes.Role)?.Value ?? "Uzman";
            if (IsAdminSelfContext() || string.Equals(role, "Manager", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                long? empId = long.TryParse(user?.FindFirst("EmployeeId")?.Value, out var e) ? e : (long?)null;
                var effRole = CurrentEmployeeAppRole(empId);
                if (string.Equals(effRole, "Manager", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(effRole, "Admin", StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        // Efektif rol: normalde claim; login-as (ActAs) ise temsil edilen kişinin gerçek AppRole'ü.
        protected string EffectiveRole()
        {
            var user = AuthUser;
            var role = user?.FindFirst(ClaimTypes.Role)?.Value ?? "Uzman";
            if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                long? empId = long.TryParse(user?.FindFirst("EmployeeId")?.Value, out var e) ? e : (long?)null;
                long? ownId = long.TryParse(user?.FindFirst("AdminOwnEmployeeId")?.Value, out var o) ? o : (long?)null;
                if (empId.HasValue && ownId.HasValue && empId != ownId) // login-as başka kişi
                    return CurrentEmployeeAppRole(empId) ?? "Uzman";
            }
            return role;
        }

        // Login-as ile temsil edilen kişinin gerçek AppRole'ü (istek başına cache'li).
        protected string CurrentEmployeeAppRole(long? employeeId)
        {
            if (_empRoleLoaded) return _currentEmpRole;
            _empRoleLoaded = true;
            if (employeeId.HasValue)
                _currentEmpRole = AuthEmployeeRepository.GetAll()
                    .Where(e => e.Id == employeeId.Value).Select(e => e.AppRole).FirstOrDefault();
            return _currentEmpRole;
        }

        protected long? CurrentEmployeeTeamId(long? employeeId)
        {
            if (_teamIdLoaded) return _currentTeamId;
            _teamIdLoaded = true;
            if (employeeId.HasValue)
                _currentTeamId = AuthEmployeeRepository.GetAll()
                    .Where(e => e.Id == employeeId.Value).Select(e => e.TeamId).FirstOrDefault();
            return _currentTeamId;
        }
    }
}
