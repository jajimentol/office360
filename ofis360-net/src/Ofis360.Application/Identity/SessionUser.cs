namespace Ofis360.Application.Identity;

/// <summary>Kaynak: server/session.ts SessionUser. Oturumdaki kullanicinin ekranlara tasinan ozeti.</summary>
public sealed record SessionUser(
    string Id,
    string Email,
    string FullName,
    string Title,
    string Role,
    string DepartmentId,
    string DepartmentName,
    bool AccessibilityApproved,
    string? PreferredFloorId,
    bool PrefersQuietZone,
    bool NotificationsEnabled,
    string? Grade = null,
    bool IsSecretary = false);
