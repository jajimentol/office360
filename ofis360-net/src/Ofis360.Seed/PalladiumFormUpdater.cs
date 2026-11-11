using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ofis360.Domain.Common;
using Ofis360.Domain.Entities;
using Ofis360.Infrastructure.Persistence;

namespace Ofis360.Seed;

public sealed class PalladiumFormReport
{
    public bool FloorUpdated { get; set; }
    public int ZonesUpdated { get; set; }
    public bool ZoneCreated { get; set; }
    public bool RoomDeactivated { get; set; }
    public bool ElementCreated { get; set; }
    public int DeskReservationsCancelled { get; set; }
    public int RoomReservationsCancelled { get; set; }
    public int DemoReservationsCancelled { get; set; }

    public bool HasChanges =>
        FloorUpdated || ZonesUpdated > 0 || ZoneCreated || RoomDeactivated || ElementCreated
        || DeskReservationsCancelled > 0 || RoomReservationsCancelled > 0 || DemoReservationsCancelled > 0;
}

/// <summary>
/// Ek Bina Faz 1 formunu kurulu bir veritabanina uygular. Idempotent ve yalnizca hedefli guncelleme yapar:
/// kroki ogeleri, masa ve oda konumlari (elle yapilan duzenlemeler) degismez, hicbir kayit silinmez.
/// PT36-B04 odasinin gecmis rezervasyonlari oldugu icin oda silinmez; pasife alinip krokiden kaldirilir.
/// Kapanan alanlardaki gelecek tarihli rezervasyonlar gerekceyle iptal edilir, gecmis kayitlara dokunulmaz.
/// </summary>
public static class PalladiumFormUpdater
{
    public const string ClosedRoomCode = "PT36-B04";
    public const string CancelReason = "Alan rezervasyona kapatıldı.";

    private const string ClosedRoomNotes = "36-B-04 Bölge Müdürü. Ek Bina Faz 1 formu gereği rezervasyona kapatıldı; kayıt geçmiş rezervasyonlar için saklanıyor.";
    private const string ClosedAreaLabel = "36-B-04 Bölge Müdürü";
    private const string ClosedAreaZoneCode = "B04";
    private const string ReferenceLabel = "36-A-04 Bölge Müdürü";

    private static readonly string[] ClosedDeskZoneCodes = ["A03", "B03"];

    public static async Task<PalladiumFormReport> ApplyAsync(Ofis360DbContext db, string todayKey)
    {
        var report = new PalladiumFormReport();
        var spec = PalladiumPlan.Floor;
        var floor = await db.Floors.SingleOrDefaultAsync(f => f.Building.Code == PalladiumPlan.Building.Code && f.Number == spec.Number)
            ?? throw new InvalidOperationException("Palladium 36. kat bulunamadi; once 'palladium' komutunu calistirin.");

        var now = DateTimeOffset.UtcNow;
        var today = DayKeys.ToDateOnly(todayKey);

        await using var transaction = await db.Database.BeginTransactionAsync();

        if (floor.Name != spec.Name || floor.Description != spec.Description)
        {
            floor.Name = spec.Name;
            floor.Description = spec.Description;
            floor.UpdatedAt = now;
            report.FloorUpdated = true;
        }

        foreach (var code in ClosedDeskZoneCodes)
        {
            var zoneSpec = PalladiumPlan.Zones.Single(z => z.Code == code);
            var zone = await db.Zones.SingleAsync(z => z.FloorId == floor.Id && z.Code == code);
            if (zone.Name == zoneSpec.Name && zone.Kind == zoneSpec.Kind && zone.Reservable == zoneSpec.Reservable) continue;
            zone.Name = zoneSpec.Name;
            zone.Kind = zoneSpec.Kind;
            zone.Reservable = zoneSpec.Reservable;
            report.ZonesUpdated++;
        }

        var room = await db.MeetingRooms.SingleOrDefaultAsync(r => r.FloorId == floor.Id && r.Code == ClosedRoomCode);

        // Bolge ve kroki ogesi odanin BUGUNKU konumunu kullanir; oda elle tasinmis olabilir.
        var areaSpec = PalladiumPlan.Zones.Single(z => z.Code == ClosedAreaZoneCode);
        var planRect = PalladiumPlan.RectFromPx(areaSpec.Px[0], areaSpec.Px[1], areaSpec.Px[2], areaSpec.Px[3]);
        var area = room is null ? planRect : new PalladiumPlan.Rect(room.X, room.Y, room.Width, room.Height);

        if (!await db.Zones.AnyAsync(z => z.FloorId == floor.Id && z.Code == ClosedAreaZoneCode))
        {
            db.Zones.Add(new Zone
            {
                FloorId = floor.Id, Code = areaSpec.Code, Name = areaSpec.Name, Kind = areaSpec.Kind,
                X = area.X, Y = area.Y, Width = area.Width, Height = area.Height,
                WindowSide = areaSpec.WindowSide, Quiet = areaSpec.Quiet, Reservable = areaSpec.Reservable,
            });
            report.ZoneCreated = true;
        }

        // Anadolu Bolge Mudurlugu guncellemesi uygulanmissa 36-B-04 artik Galata'dir; eski etiket geri eklenmez.
        var anadoluApplied = await db.FloorPlanElements.AnyAsync(e => e.FloorId == floor.Id && e.Label == PalladiumPlan.AnadoluAreaName);
        if (!anadoluApplied && !await db.FloorPlanElements.AnyAsync(e => e.FloorId == floor.Id && e.Label == ClosedAreaLabel))
        {
            var reference = await db.FloorPlanElements.AsNoTracking().FirstOrDefaultAsync(e => e.FloorId == floor.Id && e.Label == ReferenceLabel);
            db.FloorPlanElements.Add(new FloorPlanElement
            {
                FloorId = floor.Id, Type = "RESTRICTED", Label = ClosedAreaLabel,
                X = area.X, Y = area.Y, Width = area.Width, Height = area.Height,
                LayerOrder = reference?.LayerOrder ?? 100,
            });
            report.ElementCreated = true;
        }

        if (room is { } closedRoom && (closedRoom.Active || closedRoom.Placed))
        {
            closedRoom.Active = false;
            closedRoom.Placed = false;
            closedRoom.Notes = ClosedRoomNotes;
            closedRoom.UpdatedAt = now;
            report.RoomDeactivated = true;
        }

        // Yalnizca BOOKED iptal edilir; CHECKED_IN gercek bir kullanimi gosterir, ona dokunulmaz.
        var deskReservations = await db.DeskReservations
            .Where(r => r.Desk.FloorId == floor.Id && ClosedDeskZoneCodes.Contains(r.Desk.Zone.Code)
                && r.Date >= today && r.Status == DeskReservationStatus.Booked)
            .ToListAsync();
        foreach (var reservation in deskReservations)
        {
            reservation.Status = DeskReservationStatus.Cancelled;
            reservation.ActiveDeskKey = null;
            reservation.ActiveUserDayKey = null;
            reservation.CancelledAt = now;
            reservation.CancelReason = CancelReason;
            reservation.UpdatedAt = now;
        }
        report.DeskReservationsCancelled = deskReservations.Count;

        var roomReservations = await db.RoomReservations
            .Where(r => r.Room.FloorId == floor.Id && r.Room.Code == ClosedRoomCode
                && r.Date >= today && r.Status == RoomReservationStatus.Booked)
            .ToListAsync();
        foreach (var reservation in roomReservations)
        {
            reservation.Status = RoomReservationStatus.Cancelled;
            reservation.CancelledAt = now;
            reservation.CancelReason = CancelReason;
            reservation.UpdatedAt = now;
        }
        report.RoomReservationsCancelled = roomReservations.Count;

        report.DemoReservationsCancelled = await CancelInactiveDemoReservationsAsync(db, today, now);

        if (report.HasChanges)
        {
            db.AuditLogs.Add(new AuditLog
            {
                ActorName = "Sistem (Ek Bina formu)",
                ActorRole = "SYSTEM",
                Action = AuditActions.ZoneUpdate,
                EntityType = "Floor",
                EntityId = floor.Id,
                Summary = $"Ek Bina Faz 1 formu uygulandı: {report.ZonesUpdated} bölge kapatıldı, {report.DeskReservationsCancelled} masa ve {report.RoomReservationsCancelled} oda rezervasyonu iptal edildi.",
                Metadata = JsonSerializer.Serialize(report),
                CreatedAt = now,
            });
        }

        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return report;
    }

    /// <summary>Pasif demo binadaki gelecek rezervasyonlar gorunmez ama kisinin "ayni gun tek masa" anahtarini tutar; iptal edilir.</summary>
    public static async Task<int> CancelInactiveDemoReservationsAsync(Ofis360DbContext db, DateOnly today, DateTimeOffset now)
    {
        const string Reason = "Demo bina kullanımdan kaldırıldı.";
        var desks = await db.DeskReservations
            .Where(r => r.Date >= today && r.Status == DeskReservationStatus.Booked && !r.Desk.Floor.Building.Active && r.Desk.Floor.Building.IsDemo)
            .ExecuteUpdateAsync(u => u
                .SetProperty(r => r.Status, DeskReservationStatus.Cancelled)
                .SetProperty(r => r.ActiveDeskKey, (string?)null)
                .SetProperty(r => r.ActiveUserDayKey, (string?)null)
                .SetProperty(r => r.CancelledAt, now)
                .SetProperty(r => r.CancelReason, Reason)
                .SetProperty(r => r.UpdatedAt, now));
        var rooms = await db.RoomReservations
            .Where(r => r.Date >= today && r.Status == RoomReservationStatus.Booked && !r.Room.Floor.Building.Active && r.Room.Floor.Building.IsDemo)
            .ExecuteUpdateAsync(u => u
                .SetProperty(r => r.Status, RoomReservationStatus.Cancelled)
                .SetProperty(r => r.CancelledAt, now)
                .SetProperty(r => r.CancelReason, Reason)
                .SetProperty(r => r.UpdatedAt, now));
        return desks + rooms;
    }

    public static void PrintReport(TextWriter log, PalladiumFormReport report)
    {
        log.WriteLine($"Kat adi/aciklama guncellendi : {report.FloorUpdated}");
        log.WriteLine($"Kapatilan bolge (A03/B03)    : {report.ZonesUpdated}");
        log.WriteLine($"B04 bolgesi olusturuldu      : {report.ZoneCreated}");
        log.WriteLine($"B04 kroki ogesi eklendi      : {report.ElementCreated}");
        log.WriteLine($"{ClosedRoomCode} pasife alindi       : {report.RoomDeactivated}");
        log.WriteLine($"Iptal edilen masa rez.       : {report.DeskReservationsCancelled}");
        log.WriteLine($"Iptal edilen oda rez.        : {report.RoomReservationsCancelled}");
        log.WriteLine($"Iptal edilen demo bina rez.  : {report.DemoReservationsCancelled}");
        log.WriteLine(report.HasChanges ? "Degisiklikler uygulandi." : "Degisiklik yok (zaten uygulanmis).");
    }
}
