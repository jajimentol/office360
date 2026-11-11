using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ofis360.Domain.Common;
using Ofis360.Domain.Entities;
using Ofis360.Infrastructure.Persistence;

namespace Ofis360.Seed;

public sealed class PalladiumAnadoluReport
{
    public bool GalataMoved { get; set; }
    public int ElementsRemoved { get; set; }
    public bool AreaElementCreated { get; set; }
    public int ZonesUpdated { get; set; }
    public int DeskReservationsCancelled { get; set; }

    public bool HasChanges => GalataMoved || ElementsRemoved > 0 || AreaElementCreated || ZonesUpdated > 0 || DeskReservationsCancelled > 0;
}

/// <summary>
/// 36. kat yerlesim degisikligini kurulu bir veritabanina uygular (11 Kasim 2026 talebi):
/// Galata odasi 36-B-04 yerine tasinir; mutfagin batisindaki tum alan (36-A-04, 36-A-03 ve eski Galata yeri)
/// Anadolu Bolge Mudurlugu'ne ait, rezervasyona kapali ve kroki uzerinde kirmizi taramali tek bir alan olur.
/// Idempotent: tekrar calistirmak bir sey degistirmez. Galata'nin rezervasyonlari gecerli kalir (oda yalnizca tasinir).
/// </summary>
public static class PalladiumAnadoluUpdater
{
    public const string GalataCode = "PT36-A02";
    public const string CancelReason = "Alan Anadolu Bölge Müdürlüğü'ne ayrıldı.";

    /// <summary>Yeni alanla cakisan eski etiketler kaldirilir (alan ve uzerindeki yazi bunlarin yerini alir).</summary>
    private static readonly string[] ObsoleteLabels = ["36-A-04 Bölge Müdürü", "36-B-04 Bölge Müdürü", "36-A-03 Ortak çalışma alanı"];

    private static readonly string[] AreaZoneCodes = ["A03", "A04", "B04"];

    private static readonly JsonSerializerOptions Json = new() { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };

    public static async Task<PalladiumAnadoluReport> ApplyAsync(Ofis360DbContext db, string todayKey)
    {
        var report = new PalladiumAnadoluReport();
        var floor = await db.Floors.SingleOrDefaultAsync(f => f.Building.Code == PalladiumPlan.Building.Code && f.Number == PalladiumPlan.Floor.Number)
            ?? throw new InvalidOperationException("Palladium 36. kat bulunamadi; once 'palladium' komutunu calistirin.");
        var now = DateTimeOffset.UtcNow;
        var today = DayKeys.ToDateOnly(todayKey);

        await using var transaction = await db.Database.BeginTransactionAsync();

        // 1) Galata -> 36-B-04
        var target = Rect(PalladiumPlan.GalataNewPx);
        var galata = await db.MeetingRooms.SingleOrDefaultAsync(r => r.FloorId == floor.Id && r.Code == GalataCode);
        if (galata is not null && (galata.X != target.X || galata.Y != target.Y || galata.Width != target.Width || galata.Height != target.Height || !galata.Placed))
        {
            galata.X = target.X; galata.Y = target.Y; galata.Width = target.Width; galata.Height = target.Height;
            galata.Placed = true;
            galata.Notes = PalladiumPlan.GalataNotes;
            galata.UpdatedAt = now;
            report.GalataMoved = true;
        }

        // 2) Eski etiketli alanlar kaldirilir, yerine tek taramali alan eklenir.
        var obsolete = await db.FloorPlanElements.Where(e => e.FloorId == floor.Id && e.Label != null && ObsoleteLabels.Contains(e.Label)).ToListAsync();
        db.FloorPlanElements.RemoveRange(obsolete);
        report.ElementsRemoved = obsolete.Count;

        if (!await db.FloorPlanElements.AnyAsync(e => e.FloorId == floor.Id && e.Label == PalladiumPlan.AnadoluAreaName))
        {
            var area = Rect(PalladiumPlan.AnadoluAreaPx);
            db.FloorPlanElements.Add(new FloorPlanElement
            {
                FloorId = floor.Id, Type = "RESTRICTED", Label = PalladiumPlan.AnadoluAreaName,
                X = area.X, Y = area.Y, Width = area.Width, Height = area.Height,
                Style = JsonSerializer.Serialize(new { fill = PalladiumPlan.HatchRedFill, stroke = "#f87171", fontSize = 22 }, Json),
                LayerOrder = 600,
            });
            report.AreaElementCreated = true;
        }

        // 3) Bolgeler: ad ve kapalilik; B04 eski Galata yerine tasinir.
        foreach (var code in AreaZoneCodes)
        {
            var spec = PalladiumPlan.Zones.Single(z => z.Code == code);
            var rect = Rect(spec.Px);
            var zone = await db.Zones.SingleOrDefaultAsync(z => z.FloorId == floor.Id && z.Code == code);
            if (zone is null)
            {
                db.Zones.Add(new Zone
                {
                    FloorId = floor.Id, Code = code, Name = spec.Name, Kind = spec.Kind, X = rect.X, Y = rect.Y, Width = rect.Width, Height = rect.Height,
                    WindowSide = spec.WindowSide, Quiet = spec.Quiet, Reservable = false,
                });
                report.ZonesUpdated++;
                continue;
            }
            var moveZone = code == "B04" && (zone.X != rect.X || zone.Y != rect.Y || zone.Width != rect.Width || zone.Height != rect.Height);
            if (zone.Name == spec.Name && !zone.Reservable && zone.Kind == spec.Kind && !moveZone) continue;
            zone.Name = spec.Name;
            zone.Kind = spec.Kind;
            zone.Reservable = false;
            if (moveZone) { zone.X = rect.X; zone.Y = rect.Y; zone.Width = rect.Width; zone.Height = rect.Height; }
            report.ZonesUpdated++;
        }

        // 4) Alandaki masalarin gelecek tarihli (BOOKED) rezervasyonlari iptal edilir; CHECKED_IN ve gecmise dokunulmaz.
        var reservations = await db.DeskReservations
            .Where(r => r.Desk.FloorId == floor.Id && AreaZoneCodes.Contains(r.Desk.Zone.Code) && r.Date >= today && r.Status == DeskReservationStatus.Booked)
            .ToListAsync();
        foreach (var r in reservations)
        {
            r.Status = DeskReservationStatus.Cancelled;
            r.ActiveDeskKey = null;
            r.ActiveUserDayKey = null;
            r.CancelledAt = now;
            r.CancelReason = CancelReason;
            r.UpdatedAt = now;
        }
        report.DeskReservationsCancelled = reservations.Count;

        if (report.HasChanges)
        {
            db.AuditLogs.Add(new AuditLog
            {
                ActorName = "Sistem (yerleşim güncellemesi)", ActorRole = "SYSTEM", Action = AuditActions.ZoneUpdate,
                EntityType = "Floor", EntityId = floor.Id,
                Summary = $"36. kat güncellendi: Galata 36-B-04'e taşındı, mutfağın batısı {PalladiumPlan.AnadoluAreaName} olarak rezervasyona kapatıldı.",
                Metadata = JsonSerializer.Serialize(report), CreatedAt = now,
            });
        }

        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return report;
    }

    private static PalladiumPlan.Rect Rect(double[] px) => PalladiumPlan.RectFromPx(px[0], px[1], px[2], px[3]);

    public static void PrintReport(TextWriter log, PalladiumAnadoluReport r)
    {
        log.WriteLine($"Galata 36-B-04'e tasindi       : {r.GalataMoved}");
        log.WriteLine($"Kaldirilan eski kroki ogesi    : {r.ElementsRemoved}");
        log.WriteLine($"Anadolu alani eklendi          : {r.AreaElementCreated}");
        log.WriteLine($"Guncellenen bolge              : {r.ZonesUpdated}");
        log.WriteLine($"Iptal edilen masa rez.         : {r.DeskReservationsCancelled}");
        log.WriteLine(r.HasChanges ? "Degisiklikler uygulandi." : "Degisiklik yok (zaten uygulanmis).");
    }
}
