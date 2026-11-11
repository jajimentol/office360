using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Ofis360.Application.Common;
using Ofis360.Application.Persistence;
using Ofis360.Domain.Common;
using Ofis360.Domain.Entities;
using Ofis360.Domain.Plan;
using Ofis360.Domain.Rules;

namespace Ofis360.Application.Services;

// Kaynak: availability.service.ts gorunum tipleri

public sealed record FeatureView(string Key, string Name, string Icon);

public sealed record Occupant([property: JsonIgnore] string UserId, string FullName, string DepartmentName);

public sealed record DeskView(
    string Id, string Code, int X, int Y, int Width, int Height, int Rotation,
    string ZoneId, string ZoneName, bool ZoneQuiet, bool ZoneWindowSide,
    string FloorId, int FloorNumber, bool Popular, bool AccessibleOnly, bool Active, string? Notes,
    IReadOnlyList<FeatureView> Features, string State, bool Selectable, Occupant? Occupant,
    IReadOnlyList<RuleViolation> Violations, string? ClosureNote);

public sealed record ZoneView(
    string Id, string Code, string Name, string Kind, int X, int Y, int Width, int Height, bool WindowSide, bool Quiet, bool Reservable);

public sealed record RoomView(
    string Id, string Code, string Name, int Capacity, int X, int Y, int Width, int Height, bool Active,
    IReadOnlyList<FeatureView> Features, bool Closed);

public sealed record FloorPlanFloorInfo(
    string Id, int Number, string Name, string Description, bool Reservable, int PlanWidth, int PlanHeight,
    string BuildingId, string BuildingName, string BuildingCode, bool BuildingActive);

public sealed record FloorPlanSummary(int Total, int Available, int Reserved, int Closed, int Restricted, double OccupancyRate);

public sealed record FloorPlanView(
    FloorPlanFloorInfo Floor, IReadOnlyList<ZoneView> Zones, IReadOnlyList<PlanElement> Elements,
    IReadOnlyList<DeskView> Desks, IReadOnlyList<RoomView> Rooms, FloorPlanSummary Summary);

public sealed record FloorSummary(
    string Id, int Number, string Name, string Description, bool Reservable,
    string BuildingId, string BuildingName, string BuildingCode,
    int TotalDesks, int ReservedDesks, int AvailableDesks, int RoomCount, double OccupancyRate);

/// <param name="WeeklyReservationCount">Kural icin: degerlendirilen gun haric haftalik ofis gunu. "Gelmedi" sonrasi ayni gune alinan kayit ikinci gun sayilmaz.</param>
/// <param name="WeeklyTotal">Ekran icin: o gun dahil haftalik ofis gunu.</param>
public sealed record UserDeskContext(IReadOnlyList<UserDeskHistoryItem> History, SameDayReservation? SameDayReservation, int WeeklyReservationCount, int WeeklyTotal);

public sealed record ClosureMaps(
    IReadOnlyDictionary<string, string> DeskClosures,
    IReadOnlyDictionary<string, string> RoomClosures,
    IReadOnlyDictionary<string, string> FloorClosures);

/// <summary>Masa uygunlugu ve kat plani servisi: kural motoruna girdi hazirlar, her masa icin gorsel durum + gerekce uretir.</summary>
public sealed class AvailabilityService(IOfis360Db db, RuleService rules, IClock clock)
{
    private static readonly string[] OccupyingDesk = DeskReservationStatus.Occupying.ToArray();
    private static readonly string[] HoldingDesk = DeskReservationStatus.Holding.ToArray();

    public static PlanElement ToPlanElement(FloorPlanElement e) => new(
        e.Id, e.Type, e.Label, e.X, e.Y, e.Width, e.Height, e.Rotation, PlanGeometry.ParseStyle(e.Style),
        e.LayerOrder, e.Locked, e.Visible, e.LinkedResourceId, e.LinkedResourceType);

    public static FeatureView ToFeatureView(DeskFeature f) => new(f.Key, f.Name, f.Icon);
    public static FeatureView ToFeatureView(RoomFeature f) => new(f.Key, f.Name, f.Icon);

    /// <summary>Kullaniciya ait kural degerlendirme baglamini tek seferde hazirlar.</summary>
    public async Task<UserDeskContext> BuildUserDeskContextAsync(string userId, string dayKey, CancellationToken ct = default)
    {
        var day = DayKeys.ToDateOnly(dayKey);
        var weekStart = DayKeys.ToDateOnly(DayKeys.StartOfWeek(dayKey));
        var weekEnd = DayKeys.ToDateOnly(DayKeys.EndOfWeek(dayKey));

        var history = await db.DeskReservations.AsNoTracking()
            .Where(r => r.UserId == userId && OccupyingDesk.Contains(r.Status) && r.Desk.Floor.Building.Active)
            .OrderByDescending(r => r.Date)
            .Take(90)
            .Select(r => new { r.Date, r.DeskId, r.Desk.Popular })
            .ToListAsync(ct);

        var sameDay = await db.DeskReservations.AsNoTracking()
            .Where(r => r.UserId == userId && r.Date == day && HoldingDesk.Contains(r.Status) && r.Desk.Floor.Building.Active)
            .Select(r => new { r.Id, r.Desk.Code })
            .FirstOrDefaultAsync(ct);

        var weekDates = await db.DeskReservations.AsNoTracking()
            .Where(r => r.UserId == userId && OccupyingDesk.Contains(r.Status) && r.Desk.Floor.Building.Active && r.Date >= weekStart && r.Date <= weekEnd)
            .Select(r => r.Date)
            .ToListAsync(ct);

        return new UserDeskContext(
            history.Select(h => new UserDeskHistoryItem(DayKeys.FromDateOnly(h.Date), h.DeskId, h.Popular)).ToList(),
            sameDay is null ? null : new SameDayReservation(sameDay.Code, sameDay.Id),
            weekDates.Where(d => d != day).Distinct().Count(),
            weekDates.Distinct().Count());
    }

    /// <summary>Belirli tarihte aktif olan kapatma kayitlarini toplar.</summary>
    public async Task<ClosureMaps> GetClosureMapsAsync(string dayKey, CancellationToken ct = default)
    {
        var day = DayKeys.ToDateOnly(dayKey);
        var closures = await db.Closures.AsNoTracking()
            .Where(c => c.Active && c.StartDate <= day && c.EndDate >= day)
            .ToListAsync(ct);

        var desk = new Dictionary<string, string>();
        var room = new Dictionary<string, string>();
        var floor = new Dictionary<string, string>();
        foreach (var c in closures)
        {
            if (c.Scope == ClosureScopes.Desk && c.DeskId is not null) desk[c.DeskId] = c.Note;
            if (c.Scope == ClosureScopes.Room && c.RoomId is not null) room[c.RoomId] = c.Note;
            if (c.Scope == ClosureScopes.Floor && c.FloorId is not null) floor[c.FloorId] = c.Note;
        }
        return new ClosureMaps(desk, room, floor);
    }

    public async Task<FloorPlanView> GetFloorPlanAsync(string floorId, string dayKey, DeskRuleUser user, RuleSettings? settings = null, bool viewerIsManaging = false, bool includeInactive = false, CancellationToken ct = default)
    {
        var day = DayKeys.ToDateOnly(dayKey);

        var floor = await db.Floors.AsNoTracking()
            .Include(f => f.Building)
            .Include(f => f.Zones.OrderBy(z => z.Code))
            .Include(f => f.Elements.OrderBy(e => e.LayerOrder).ThenBy(e => e.CreatedAt))
            .Include(f => f.Desks.Where(d => d.Placed).OrderBy(d => d.Code)).ThenInclude(d => d.Features).ThenInclude(l => l.Feature)
            .Include(f => f.Desks.Where(d => d.Placed).OrderBy(d => d.Code)).ThenInclude(d => d.Zone)
            .Include(f => f.Rooms.Where(r => r.Placed).OrderBy(r => r.Code)).ThenInclude(r => r.Features).ThenInclude(l => l.Feature)
            .FirstOrDefaultAsync(f => f.Id == floorId, ct)
            ?? throw AppErrors.NotFound("Kat bulunamadı."); // K1: kaynakta duz Error (500) idi
        if (!includeInactive && (!floor.Active || !floor.Building.Active)) throw AppErrors.NotFound("Kat bulunamadı.");

        settings ??= await rules.GetRuleSettingsForUserAsync(user.Id, floor.BuildingId, ct);
        if (user.ActorIsSecretary) settings = RuleService.WithSecretaryExemptions(settings);
        var calendar = await rules.GetCalendarAsync(ct);

        var reservations = await db.DeskReservations.AsNoTracking()
            .Where(r => r.Date == day && HoldingDesk.Contains(r.Status) && r.Desk.FloorId == floorId)
            .Select(r => new { r.DeskId, r.UserId, r.User.FullName, DepartmentName = r.User.Department.Name })
            .ToListAsync(ct);
        var closures = await GetClosureMapsAsync(dayKey, ct);
        var context = await BuildUserDeskContextAsync(user.Id, dayKey, ct);

        var occupancy = reservations
            .GroupBy(r => r.DeskId)
            .ToDictionary(g => g.Key, g => new Occupant(g.First().UserId, g.First().FullName, g.First().DepartmentName));

        var floorClosureNote = closures.FloorClosures.GetValueOrDefault(floor.Id);
        var today = clock.TodayKey();
        var hidePastOccupants = !viewerIsManaging && day < clock.Today;

        var desks = floor.Desks.Select(desk =>
        {
            var closureNote = closures.DeskClosures.GetValueOrDefault(desk.Id) ?? floorClosureNote;
            var occupant = occupancy.GetValueOrDefault(desk.Id);

            var ruleContext = new DeskRuleContext(
                TodayKey: today, DayKey: dayKey, User: user,
                Desk: new DeskRuleDesk(desk.Id, desk.Code, desk.Active, desk.Placed, desk.AccessibleOnly, desk.Popular,
                    floor.Name, floor.Reservable, desk.Zone.Name, desk.Zone.Reservable, floor.Building.Active, floor.Building.Name),
                Closure: new ClosureInfo(closureNote is not null, closureNote),
                OccupiedBy: occupant is null ? null : new OccupiedBy(occupant.UserId, occupant.FullName),
                SameDayReservation: context.SameDayReservation,
                WeeklyReservationCount: context.WeeklyReservationCount,
                History: context.History,
                Calendar: calendar);

            var evaluation = DeskRules.Evaluate(ruleContext, settings);

            var state = DeskViewStates.Available;
            if (!desk.Active || closureNote is not null || !desk.Zone.Reservable) state = DeskViewStates.Closed;
            else if (occupant is not null && occupant.UserId == user.Id) state = DeskViewStates.Mine;
            else if (occupant is not null) state = DeskViewStates.Reserved;
            else if (desk.AccessibleOnly) state = DeskViewStates.Accessible;
            else if (!evaluation.Allowed) state = DeskViewStates.RestrictedByRule;

            return new DeskView(desk.Id, desk.Code, desk.X, desk.Y, desk.Width, desk.Height, desk.Rotation,
                desk.ZoneId, desk.Zone.Name, desk.Zone.Quiet, desk.Zone.WindowSide, floor.Id, floor.Number,
                desk.Popular, desk.AccessibleOnly, desk.Active, desk.Notes,
                desk.Features.Select(l => ToFeatureView(l.Feature)).ToList(),
                state, evaluation.Allowed, hidePastOccupants && occupant?.UserId != user.Id ? null : occupant, evaluation.Violations, closureNote);
        }).ToList();

        // Rezervasyona kapali bolgedeki masalar (Closed) kapasiteye sayilmaz.
        var reservableDesks = floor.Reservable && floor.Building.Active ? desks.Count(d => d.State != DeskViewStates.Closed) : 0;
        var reserved = desks.Count(d => d.State is DeskViewStates.Mine or DeskViewStates.Reserved);
        var closed = desks.Count(d => d.State == DeskViewStates.Closed);
        var available = desks.Count(d => d.State is DeskViewStates.Available or DeskViewStates.Accessible);
        var restricted = desks.Count(d => d.State == DeskViewStates.RestrictedByRule);

        return new FloorPlanView(
            new FloorPlanFloorInfo(floor.Id, floor.Number, floor.Name, floor.Description,
                floor.Reservable && floor.Building.Active, floor.PlanWidth, floor.PlanHeight,
                floor.BuildingId, floor.Building.Name, floor.Building.Code, floor.Building.Active),
            floor.Zones.Select(z => new ZoneView(z.Id, z.Code, z.Name, z.Kind, z.X, z.Y, z.Width, z.Height, z.WindowSide, z.Quiet, z.Reservable)).ToList(),
            floor.Elements.Select(ToPlanElement).ToList(),
            desks,
            floor.Rooms.Select(room => new RoomView(room.Id, room.Code, room.Name, room.Capacity, room.X, room.Y, room.Width, room.Height, room.Active,
                room.Features.Select(l => ToFeatureView(l.Feature)).ToList(),
                closures.RoomClosures.ContainsKey(room.Id) || floorClosureNote is not null || !room.Active)).ToList(),
            new FloorPlanSummary(reservableDesks, available, reserved, closed, restricted,
                reservableDesks > 0 ? (double)reserved / reservableDesks * 100 : 0));
    }

    /// <summary>Katlarin gunluk uygunluk ozeti - calisan ana sayfasinda kullanilir.</summary>
    public async Task<List<FloorSummary>> GetFloorSummariesAsync(string dayKey, CancellationToken ct = default)
    {
        var day = DayKeys.ToDateOnly(dayKey);

        var floors = await db.Floors.AsNoTracking()
            .Where(f => f.Active && f.Building.Active)
            .OrderBy(f => f.Building.Order).ThenBy(f => f.Number)
            .Select(f => new
            {
                f.Id, f.Number, f.Name, f.Description, f.Reservable,
                BuildingId = f.Building.Id, BuildingName = f.Building.Name, BuildingCode = f.Building.Code,
                Desks = f.Desks.Where(d => d.Zone.Reservable && d.Placed).Select(d => new { d.Id, d.Active }).ToList(),
                RoomCount = f.Rooms.Count(r => r.Placed && r.Active),
            })
            .ToListAsync(ct);

        var reservedDeskIds = (await db.DeskReservations.AsNoTracking()
            .Where(r => r.Date == day && HoldingDesk.Contains(r.Status))
            .Select(r => r.DeskId)
            .Distinct()
            .ToListAsync(ct)).ToHashSet();
        var closures = await GetClosureMapsAsync(dayKey, ct);

        return floors.Select(floor =>
        {
            var usable = floor.Desks.Where(d => d.Active && !closures.DeskClosures.ContainsKey(d.Id) && !closures.FloorClosures.ContainsKey(floor.Id)).ToList();
            var reserved = usable.Count(d => reservedDeskIds.Contains(d.Id));
            return new FloorSummary(floor.Id, floor.Number, floor.Name, floor.Description, floor.Reservable,
                floor.BuildingId, floor.BuildingName, floor.BuildingCode,
                usable.Count, reserved, usable.Count - reserved, floor.RoomCount,
                usable.Count > 0 ? (double)reserved / usable.Count * 100 : 0);
        }).ToList();
    }
}
