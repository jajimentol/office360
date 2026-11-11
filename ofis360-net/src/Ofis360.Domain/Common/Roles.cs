namespace Ofis360.Domain.Common;

/// <summary>Kullanici rolleri (kaynak: lib/enums.ts ROLES).</summary>
public static class Roles
{
    public const string Employee = "EMPLOYEE";
    public const string Manager = "MANAGER";
    public const string Admin = "ADMIN";

    public static readonly IReadOnlyList<string> All = [Employee, Manager, Admin];

    public static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>
    {
        [Employee] = "Çalışan",
        [Manager] = "Yönetici",
        [Admin] = "Yönetim (Admin)",
    };

    public static bool IsValid(string role) => All.Contains(role);
}
