using Ofis360.Application.Identity;
using Ofis360.Application.Services;
using Ofis360.Domain.Common;
using Ofis360.Domain.Entities;

namespace Ofis360.Api.Contracts;

// Kaynaktaki Prisma include'li donuslerin JSON karsiliklari. Alan adlari kaynakla ayni (camelCase).

public sealed record DepartmentDto(string Id, string Code, string Name, string Color, bool Active, int? UserCount = null);
public sealed record BuildingDto(string Id, string Code, string Name, string Address, int Order, bool Active, bool IsDemo, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, int? FloorCount = null);
public sealed record FloorRefDto(string Id, int Number, string Name, bool Reservable, bool Active, string BuildingId, int PlanWidth, int PlanHeight);
public sealed record ZoneDto(string Id, string FloorId, string Code, string Name, string Kind, int X, int Y, int Width, int Height, bool WindowSide, bool Quiet, bool Reservable);
public sealed record FeatureDto(string Id, string Key, string Name, string Icon, string? Description = null);
public sealed record UserRefDto(string Id, string FullName, string Email, string Title, string Role, string DepartmentId, string? DepartmentName);

public sealed record FloorDto(
    string Id, string BuildingId, int Number, string Name, string Description, bool Reservable, bool Active, int PlanWidth, int PlanHeight,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, BuildingDto? Building, IReadOnlyList<ZoneDto>? Zones, int? DeskCount, int? RoomCount, string Label);

public sealed record DeskDto(
    string Id, string Code, string FloorId, string ZoneId, int X, int Y, int Width, int Height, int Rotation, bool Placed, bool Active,
    bool AccessibleOnly, bool Popular, string? Notes, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
    FloorRefDto? Floor, ZoneDto? Zone, IReadOnlyList<FeatureDto> Features, int? ReservationCount = null);

public sealed record RoomDto(
    string Id, string Code, string Name, string FloorId, int Capacity, int X, int Y, int Width, int Height, bool Placed, bool Active, string? Notes,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, FloorRefDto? Floor, IReadOnlyList<FeatureDto> Features, int? ReservationCount = null);

public sealed record DeskReservationDto(
    string Id, string DeskId, string UserId, string Date, string Status, string? Note, string Source, DateTimeOffset? CheckedInAt,
    DateTimeOffset? CancelledAt, string? CancelledById, string? CancelReason, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
    DeskDto? Desk, UserRefDto? User);

public sealed record RoomReservationDto(
    string Id, string RoomId, string UserId, string Date, DateTimeOffset StartAt, DateTimeOffset EndAt, int StartMinutes, int EndMinutes,
    string Title, string Purpose, int AttendeeCount, string Status, string Source, DateTimeOffset? CancelledAt, string? CancelledById,
    string? CancelReason, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, RoomDto? Room, UserRefDto? User, DateTimeOffset? StartedAt = null);

public sealed record UserDto(
    string Id, string Email, string FullName, string Title, string Role, string RoleLabel, string DepartmentId, bool AccessibilityApproved,
    bool IsDemoAccount, bool Active, string? PreferredFloorId, bool PrefersQuietZone, bool NotificationsEnabled, DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt, DepartmentDto? Department, string? Grade, string? GradeLabel, bool IsSecretary,
    int? DeskReservationCount = null, int? RoomReservationCount = null);

public sealed record RuleDto(
    string Id, string Key, string Name, string Description, string Category, bool Enabled, bool EditableLimit, int? IntValue, int? SecondValue,
    string? Unit, int? MinValue, int? MaxValue, bool SystemRule, int Order, DateTimeOffset UpdatedAt, string? UpdatedById);

public sealed record SettingDto(string Id, string Key, string Value, string Label, string Description, string Type, string Group, int Order, DateTimeOffset UpdatedAt);

public sealed record ClosureDto(
    string Id, string Scope, string ScopeLabel, string? FloorId, string? DeskId, string? RoomId, string StartDate, string EndDate, string Reason, string ReasonLabel,
    string Note, bool Active, string CreatedById, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, FloorRefDto? Floor, DeskDto? Desk, RoomDto? Room);

public sealed record NotificationDto(string Id, string UserId, string Type, string Title, string Body, string? Link, string Severity, DateTimeOffset? ReadAt, DateTimeOffset CreatedAt);

public sealed record AuditLogDto(string Id, string? ActorId, string ActorName, string ActorRole, string Action, string ActionLabel, string EntityType, string? EntityId, string Summary, string? Metadata, DateTimeOffset CreatedAt);

public sealed record PagedDto<T>(IReadOnlyList<T> Items, int Total, int Take, int Skip);

public static class Dto
{
    public static DepartmentDto Map(Department d, int? userCount = null) => new(d.Id, d.Code, d.Name, d.Color, d.Active, userCount);

    public static BuildingDto Map(Building b, int? floorCount = null) =>
        new(b.Id, b.Code, b.Name, b.Address, b.Order, b.Active, b.IsDemo, b.CreatedAt, b.UpdatedAt, floorCount);

    public static FloorRefDto Ref(Floor f) => new(f.Id, f.Number, f.Name, f.Reservable, f.Active, f.BuildingId, f.PlanWidth, f.PlanHeight);

    public static ZoneDto Map(Zone z) => new(z.Id, z.FloorId, z.Code, z.Name, z.Kind, z.X, z.Y, z.Width, z.Height, z.WindowSide, z.Quiet, z.Reservable);

    public static FeatureDto Map(DeskFeature f) => new(f.Id, f.Key, f.Name, f.Icon, f.Description);
    public static FeatureDto Map(RoomFeature f) => new(f.Id, f.Key, f.Name, f.Icon);

    public static UserRefDto Ref(AppUser u) => new(u.Id, u.FullName, u.Email, u.Title, u.Role, u.DepartmentId, u.Department?.Name);

    public static FloorDto Map(Floor f, int? deskCount = null, int? roomCount = null) => new(
        f.Id, f.BuildingId, f.Number, f.Name, f.Description, f.Reservable, f.Active, f.PlanWidth, f.PlanHeight, f.CreatedAt, f.UpdatedAt,
        f.Building is null ? null : Map(f.Building),
        f.Zones.Count > 0 ? f.Zones.Select(Map).ToList() : null,
        deskCount, roomCount, SpaceService.FloorLabel(f));

    public static DeskDto Map(Desk d, int? reservationCount = null) => new(
        d.Id, d.Code, d.FloorId, d.ZoneId, d.X, d.Y, d.Width, d.Height, d.Rotation, d.Placed, d.Active, d.AccessibleOnly, d.Popular, d.Notes,
        d.CreatedAt, d.UpdatedAt, d.Floor is null ? null : Ref(d.Floor), d.Zone is null ? null : Map(d.Zone),
        d.Features.Where(l => l.Feature is not null).Select(l => Map(l.Feature)).ToList(), reservationCount);

    public static RoomDto Map(MeetingRoom r, int? reservationCount = null) => new(
        r.Id, r.Code, r.Name, r.FloorId, r.Capacity, r.X, r.Y, r.Width, r.Height, r.Placed, r.Active, r.Notes, r.CreatedAt, r.UpdatedAt,
        r.Floor is null ? null : Ref(r.Floor), r.Features.Where(l => l.Feature is not null).Select(l => Map(l.Feature)).ToList(), reservationCount);

    public static DeskReservationDto Map(DeskReservation r) => new(
        r.Id, r.DeskId, r.UserId, r.DayKey, r.Status, r.Note, r.Source, r.CheckedInAt, r.CancelledAt, r.CancelledById, r.CancelReason,
        r.CreatedAt, r.UpdatedAt, r.Desk is null ? null : Map(r.Desk), r.User is null ? null : Ref(r.User));

    public static RoomReservationDto Map(RoomReservation r) => new(
        r.Id, r.RoomId, r.UserId, r.DayKey, r.StartAt, r.EndAt, r.StartMinutes, r.EndMinutes, r.Title, r.Purpose, r.AttendeeCount, r.Status, r.Source,
        r.CancelledAt, r.CancelledById, r.CancelReason, r.CreatedAt, r.UpdatedAt, r.Room is null ? null : Map(r.Room), r.User is null ? null : Ref(r.User), r.StartedAt);

    public static UserDto Map(AppUser u, int? deskCount = null, int? roomCount = null) => new(
        u.Id, u.Email, u.FullName, u.Title, u.Role, Roles.Labels.GetValueOrDefault(u.Role, u.Role), u.DepartmentId, u.AccessibilityApproved,
        u.IsDemoAccount, u.Active, u.PreferredFloorId, u.PrefersQuietZone, u.NotificationsEnabled, u.CreatedAt, u.UpdatedAt,
        u.Department is null ? null : Map(u.Department), u.Grade, Grades.Label(u.Grade), u.IsSecretary, deskCount, roomCount);

    public static RuleDto Map(ReservationRule r) => new(
        r.Id, r.Key, r.Name, r.Description, r.Category, r.Enabled, r.EditableLimit, r.IntValue, r.SecondValue, r.Unit, r.MinValue, r.MaxValue,
        r.SystemRule, r.Order, r.UpdatedAt, r.UpdatedById);

    public static SettingDto Map(SystemSetting s) => new(s.Id, s.Key, s.Value, s.Label, s.Description, s.Type, s.Group, s.Order, s.UpdatedAt);

    public static ClosureDto Map(Closure c) => new(
        c.Id, c.Scope, ClosureScopes.Labels.GetValueOrDefault(c.Scope, c.Scope), c.FloorId, c.DeskId, c.RoomId,
        DayKeys.FromDateOnly(c.StartDate), DayKeys.FromDateOnly(c.EndDate), c.Reason, ClosureReasons.Labels.GetValueOrDefault(c.Reason, c.Reason),
        c.Note, c.Active, c.CreatedById, c.CreatedAt, c.UpdatedAt,
        c.Floor is null ? null : Ref(c.Floor), c.Desk is null ? null : Map(c.Desk), c.Room is null ? null : Map(c.Room));

    public static NotificationDto Map(Notification n) => new(n.Id, n.UserId, n.Type, n.Title, n.Body, n.Link, n.Severity, n.ReadAt, n.CreatedAt);

    public static AuditLogDto Map(AuditLog a) => new(
        a.Id, a.ActorId, a.ActorName, a.ActorRole, a.Action, AuditActions.Labels.GetValueOrDefault(a.Action, a.Action), a.EntityType, a.EntityId,
        a.Summary, a.Metadata, a.CreatedAt);

    public static SessionUser Session(SessionUser s) => s;

    public static PagedDto<TOut> Map<TIn, TOut>(Application.Common.PagedResult<TIn> page, Func<TIn, TOut> map) =>
        new(page.Items.Select(map).ToList(), page.Total, page.Take, page.Skip);
}
