using Microsoft.EntityFrameworkCore;
using Ofis360.Application.Common;
using Ofis360.Application.Identity;
using Ofis360.Application.Persistence;
using Ofis360.Domain.Common;

namespace Ofis360.Application.Services;

public sealed record PresencePerson(
    string FullName, string Title, string DepartmentName, string DepartmentColor,
    string BuildingName, string FloorId, string FloorName, int FloorNumber, string DeskCode,
    string Status, bool CheckedIn, DateTimeOffset? CheckedInAt, bool IsMe);

public sealed record PresenceFilterOption(string Id, string Name);

public sealed record PresenceView(
    string Date, bool ManagersOnly, int Total, int CheckedIn,
    IReadOnlyList<PresencePerson> People, IReadOnlyList<PresenceFilterOption> Floors, IReadOnlyList<PresenceFilterOption> Departments);

/// <summary>
/// "Ofiste kim var": secilen gun aktif masa rezervasyonu olan kisiler. Toplanti odasi kayitlari sayilmaz.
/// Gorunurluk presence.managers_only ayariyla yonetilir (varsayilan: herkes).
/// </summary>
public sealed class PresenceService(IOfis360Db db)
{
    private static readonly string[] HoldingDesk = DeskReservationStatus.Holding.ToArray();

    public async Task<bool> IsManagersOnlyAsync(CancellationToken ct = default)
    {
        var value = await db.SystemSettings.AsNoTracking().Where(s => s.Key == SettingKeys.PresenceManagersOnly).Select(s => s.Value).FirstOrDefaultAsync(ct);
        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    public static bool CanView(SessionUser user, bool managersOnly) =>
        !managersOnly || user.Role is Roles.Manager or Roles.Admin;

    public async Task<PresenceView> GetAsync(SessionUser viewer, string dayKey, string? floorId, string? departmentId, string? search, CancellationToken ct = default)
    {
        var managersOnly = await IsManagersOnlyAsync(ct);
        if (!CanView(viewer, managersOnly)) throw AppErrors.Forbidden();

        var day = DayKeys.ToDateOnly(dayKey);
        var rows = await db.DeskReservations.AsNoTracking()
            .Where(r => r.Date == day && HoldingDesk.Contains(r.Status) && r.Desk.Floor.Active && r.Desk.Floor.Building.Active)
            .Select(r => new
            {
                r.UserId, r.User.FullName, r.User.Title, DepartmentId = r.User.DepartmentId, DepartmentName = r.User.Department.Name, DepartmentColor = r.User.Department.Color,
                BuildingName = r.Desk.Floor.Building.Name, BuildingOrder = r.Desk.Floor.Building.Order, r.Desk.FloorId, FloorName = r.Desk.Floor.Name, FloorNumber = r.Desk.Floor.Number,
                DeskCode = r.Desk.Code, r.Status, r.CheckedInAt,
            })
            .ToListAsync(ct);

        // Filtre secenekleri o gunun tum kayitlarindan gelir; filtre uygulandiktan sonra secenek kaybolmaz.
        var floors = rows.OrderBy(r => r.BuildingOrder).ThenBy(r => r.FloorNumber)
            .Select(r => new PresenceFilterOption(r.FloorId, $"{r.BuildingName} · {r.FloorName}")).DistinctBy(f => f.Id).ToList();
        var departments = rows.Select(r => new PresenceFilterOption(r.DepartmentId, r.DepartmentName)).DistinctBy(d => d.Id)
            .OrderBy(d => d.Name, StringComparer.Create(new System.Globalization.CultureInfo("tr-TR"), true)).ToList();

        var filtered = rows.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(floorId)) filtered = filtered.Where(r => r.FloorId == floorId);
        if (!string.IsNullOrWhiteSpace(departmentId)) filtered = filtered.Where(r => r.DepartmentId == departmentId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var culture = new System.Globalization.CultureInfo("tr-TR");
            var needle = search.Trim().ToLower(culture);
            filtered = filtered.Where(r => r.FullName.ToLower(culture).Contains(needle) || r.DeskCode.ToLower(culture).Contains(needle) || r.DepartmentName.ToLower(culture).Contains(needle));
        }

        var trCompare = StringComparer.Create(new System.Globalization.CultureInfo("tr-TR"), true);
        var people = filtered
            .OrderBy(r => r.FullName, trCompare)
            .Select(r =>
            {
                var checkedIn = r.Status is DeskReservationStatus.CheckedIn or DeskReservationStatus.Completed || r.CheckedInAt is not null;
                return new PresencePerson(r.FullName, r.Title, r.DepartmentName, r.DepartmentColor, r.BuildingName, r.FloorId, r.FloorName, r.FloorNumber,
                    r.DeskCode, r.Status, checkedIn, r.CheckedInAt, r.UserId == viewer.Id);
            })
            .ToList();

        return new PresenceView(dayKey, managersOnly, people.Count, people.Count(p => p.CheckedIn), people, floors, departments);
    }
}
