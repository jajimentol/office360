using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ofis360.Api.Contracts;
using Ofis360.Api.Identity;
using Ofis360.Application.Common;
using Ofis360.Application.Persistence;
using Ofis360.Application.Services;
using Ofis360.Application.Validation;
using Ofis360.Domain.Common;
using Ofis360.Domain.Rules;

namespace Ofis360.Api.Controllers;

/// <summary>Calisan ana sayfasi ve kat planlari (kaynak: panel/page.tsx, panel/masa).</summary>
[Route("api")]
[RequireRole]
public sealed class PanelController(PersonalService personal, AvailabilityService availability, IClock clock, IOfis360Db db) : ApiControllerBase
{
    [HttpGet("panel/home")]
    public async Task<IActionResult> Home()
    {
        var summary = await personal.GetEmployeeHomeSummaryAsync(Session.Id, Ct);
        var floors = await availability.GetFloorSummariesAsync(summary.Today, Ct);
        return Ok(new
        {
            summary.Today,
            TodayDesk = summary.TodayDesk is null ? null : Dto.Map(summary.TodayDesk),
            NextDesk = summary.NextDesk is null ? null : Dto.Map(summary.NextDesk),
            TodayRooms = summary.TodayRooms.Select(Dto.Map),
            NextRoom = summary.NextRoom is null ? null : Dto.Map(summary.NextRoom),
            summary.FairUse,
            summary.Announcement,
            summary.AdvanceDays,
            Floors = floors,
        });
    }

    [HttpGet("floors/summaries")]
    public async Task<IActionResult> FloorSummaries([FromQuery] string? date) =>
        Ok(await availability.GetFloorSummariesAsync(DayKeyOr(date, clock.TodayKey()), Ct));

    [HttpGet("floors/{floorId}/plan")]
    public async Task<IActionResult> FloorPlan(string floorId, [FromQuery] string? date, [FromQuery] string? asUser)
    {
        var user = new DeskRuleUser(Session.Id, Session.FullName, Session.AccessibilityApproved, Session.IsSecretary);
        if (!string.IsNullOrWhiteSpace(asUser) && asUser != Session.Id)
        {
            if (Session.Role != Roles.Admin && !Session.IsSecretary) throw AppErrors.Forbidden();
            var target = await db.Users.AsNoTracking().Where(u => u.Id == asUser && u.Active)
                .Select(u => new { u.Id, u.FullName, u.AccessibilityApproved, u.IsSecretary }).FirstOrDefaultAsync(Ct)
                ?? throw AppErrors.NotFound("Kullanıcı bulunamadı.");
            user = new DeskRuleUser(target.Id, target.FullName, target.AccessibilityApproved, Session.IsSecretary || target.IsSecretary);
        }
        return Ok(await availability.GetFloorPlanAsync(floorId, DayKeyOr(date, clock.TodayKey()), user, null, ManagesReservations, Session.Role == Roles.Admin, Ct));
    }
}

/// <summary>Masa rezervasyon aksiyonlari (kaynak: reservation.actions.ts).</summary>
[Route("api/desk-reservations")]
[RequireRole]
public sealed class DeskReservationsController(DeskReservationService service) : ApiControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] DeskReservationInput? input)
    {
        var payload = Validate(new DeskReservationValidator(), input);
        var reservation = await service.CreateAsync(new CreateDeskReservationCommand(payload.DeskId, payload.Date, payload.Note, payload.TargetUserId), Session, Ct);
        return Ok(new { reservation.Id, DeskCode = reservation.Desk.Code, Date = payload.Date, Reservation = Dto.Map(reservation) });
    }

    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> Cancel(string id, [FromBody] CancelInput? input)
    {
        var payload = Validate(new CancelValidator(), input ?? new CancelInput());
        var reservation = await service.CancelAsync(id, Blank(payload.Reason), Session, Ct);
        return Ok(new { reservation.Id, Reservation = Dto.Map(reservation) });
    }

    [HttpPost("{id}/check-in")]
    public async Task<IActionResult> CheckIn(string id)
    {
        var reservation = await service.CheckInAsync(id, Session, Ct);
        return Ok(new { reservation.Id, reservation.Status, reservation.CheckedInAt });
    }
}

/// <summary>Toplanti odasi uygunlugu ve rezervasyonlari.</summary>
[Route("api")]
[RequireRole]
public sealed class RoomReservationsController(RoomReservationService service, IClock clock) : ApiControllerBase
{
    [HttpGet("rooms/availability")]
    public async Task<IActionResult> Availability(
        [FromQuery] string? date, [FromQuery] int? start, [FromQuery] int? end, [FromQuery] int? minCapacity,
        [FromQuery] string? floorId, [FromQuery] string? features)
    {
        var keys = string.IsNullOrWhiteSpace(features) ? null : features.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return Ok(await service.GetAvailabilityAsync(new RoomAvailabilityQuery(DayKeyOr(date, clock.TodayKey()), start, end, minCapacity, Blank(floorId), keys, Session.Id, Session.IsSecretary, ManagesReservations), Ct));
    }

    [HttpPost("room-reservations")]
    public async Task<IActionResult> Create([FromBody] RoomReservationInput? input)
    {
        var p = Validate(new RoomReservationValidator(), input);
        var reservation = await service.CreateAsync(
            new CreateRoomReservationCommand(p.RoomId, p.Date, p.StartMinutes, p.EndMinutes, p.Title, p.Purpose, p.AttendeeCount, p.TargetUserId), Session, Ct);
        return Ok(new { reservation.Id, RoomName = reservation.Room.Name, Reservation = Dto.Map(reservation) });
    }

    [HttpPost("room-reservations/{id}/start")]
    public async Task<IActionResult> Start(string id)
    {
        var reservation = await service.StartAsync(id, Session, Ct);
        return Ok(new { reservation.Id, reservation.StartedAt });
    }

    [HttpPost("room-reservations/{id}/cancel")]
    public async Task<IActionResult> Cancel(string id, [FromBody] CancelInput? input)
    {
        var payload = Validate(new CancelValidator(), input ?? new CancelInput());
        var reservation = await service.CancelAsync(id, Blank(payload.Reason), Session, Ct);
        return Ok(new { reservation.Id, Reservation = Dto.Map(reservation) });
    }
}

/// <summary>Calisanin kendi kayitlari: rezervasyonlar, kullanim, bildirimler, tercihler.</summary>
[Route("api/me")]
[RequireRole]
public sealed class MeController(
    DeskReservationService desks, PersonalService personal, NotificationService notifications, UserService users,
    RuleService rules, SpaceService space, IOfis360Db db, IClock clock) : ApiControllerBase
{
    /// <summary>
    /// Kaynak sayfalarin (panel/masa, panel/toplanti, panel/profil) sunucuda dogrudan Prisma ile cektigi baglam:
    /// kural ayarlari, aktif binalarin aktif katlari, oda donanimlari ve kullanicinin rezervasyon sayilari.
    /// </summary>
    [HttpGet("context")]
    public async Task<IActionResult> Context()
    {
        var settings = await rules.GetRuleSettingsForUserAsync(Session.Id, null, Ct);
        if (Session.IsSecretary) settings = RuleService.WithSecretaryExemptions(settings);
        var calendar = await rules.GetCalendarAsync(Ct);
        var today = clock.TodayKey();
        var latestBookable = settings.AdvanceBookingDays.Enabled
            ? calendar.LatestBookableDay(today, settings.AdvanceBookingDays.Days)
            : DayKeys.AddDays(today, 365);
        var floors = await db.Floors.AsNoTracking()
            .Where(f => f.Active && f.Building.Active)
            .OrderBy(f => f.Building.Order).ThenBy(f => f.Number)
            .Select(f => new
            {
                f.Id, f.Number, f.Name, f.Reservable, f.BuildingId, BuildingName = f.Building.Name, BuildingCode = f.Building.Code,
                HasRooms = f.Rooms.Any(),
            })
            .ToListAsync(Ct);
        var roomFeatures = await space.ListRoomFeaturesAsync(Ct);
        var deskCount = await db.DeskReservations.AsNoTracking().CountAsync(r => r.UserId == Session.Id && r.Status != DeskReservationStatus.Cancelled, Ct);
        var roomCount = await db.RoomReservations.AsNoTracking().CountAsync(r => r.UserId == Session.Id && r.Status != RoomReservationStatus.Cancelled, Ct);
        return Ok(new
        {
            Settings = settings,
            LatestBookableDayKey = latestBookable,
            BlockedDayKeys = settings.WorkingDaysOnly.Enabled ? calendar.NonWorkingDays(today, latestBookable) : (IReadOnlyList<string>)[],
            Floors = floors,
            RoomFeatures = roomFeatures.Select(f => new { f.Key, f.Name }),
            DeskReservationCount = deskCount,
            RoomReservationCount = roomCount,
        });
    }

    [HttpGet("reservations")]
    public async Task<IActionResult> Reservations()
    {
        var r = await desks.GetUserReservationsAsync(Session.Id, Ct);
        return Ok(new
        {
            UpcomingDesks = r.UpcomingDesks.Select(Dto.Map), PastDesks = r.PastDesks.Select(Dto.Map),
            UpcomingRooms = r.UpcomingRooms.Select(Dto.Map), PastRooms = r.PastRooms.Select(Dto.Map),
        });
    }

    [HttpGet("usage")]
    public async Task<IActionResult> Usage() => Ok(await personal.GetPersonalSummaryAsync(Session.Id, Ct));

    [HttpGet("notifications")]
    public async Task<IActionResult> Notifications([FromQuery] int? take)
    {
        var items = await notifications.ListAsync(Session.Id, Math.Clamp(take ?? 50, 1, 200), Ct);
        var unread = await notifications.CountUnreadAsync(Session.Id, Ct);
        return Ok(new { Items = items.Select(Dto.Map), Unread = unread, Channels = notifications.ListChannels().Select(c => new { c.Key, c.Enabled }) });
    }

    [HttpGet("notifications/unread-count")]
    public async Task<IActionResult> UnreadCount() => Ok(new { Unread = await notifications.CountUnreadAsync(Session.Id, Ct) });

    [HttpPost("notifications/{id}/read")]
    public async Task<IActionResult> MarkRead(string id)
    {
        await notifications.MarkReadAsync(Session.Id, id, Ct);
        return Ok(new { done = true });
    }

    [HttpPost("notifications/read-all")]
    public async Task<IActionResult> MarkAllRead()
    {
        await notifications.MarkAllReadAsync(Session.Id, Ct);
        return Ok(new { done = true });
    }

    [HttpPut("preferences")]
    public async Task<IActionResult> Preferences([FromBody] PreferencesInput? input)
    {
        if (input is null) throw AppErrors.InvalidJson();
        var user = await users.UpdateOwnPreferencesAsync(Session.Id, input, Ct);
        return Ok(new { updated = true, user = Dto.Map(user) });
    }
}
