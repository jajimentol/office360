using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ofis360.Domain.Entities;
using Ofis360.Domain.Plan;
using Ofis360.Infrastructure.Persistence;

namespace Ofis360.Seed;

public sealed class PalladiumReport
{
    public bool BuildingCreated { get; set; }
    public bool FloorCreated { get; set; }
    public int ZonesCreated { get; set; }
    public int ElementsCreated { get; set; }
    public bool ElementsSkipped { get; set; }
    public int DesksCreated { get; set; }
    public int DesksExisting { get; set; }
    public int RoomsCreated { get; set; }
    public int RoomsExisting { get; set; }
}

public sealed record PalladiumResult(Building Building, Floor Floor, PalladiumReport Report);

/// <summary>
/// Palladium Tower 36. Kat kurulumu (kaynak: prisma/seed-palladium.ts). Additive ve idempotent: hicbir kaydi silmez,
/// mevcut kayitlari ezmez. Kroki yalnizca bosken cizilir; <c>redrawSketch</c> ile yalnizca kroki ogeleri yenilenir.
/// </summary>
public static class PalladiumSeeder
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };

    private static readonly (string Key, string Name, string Description, string Icon)[] RequiredDeskFeatures =
    [
        ("WINDOW", "Pencere yanı", "Doğal ışık alan, cepheye yakın masa", "sun"),
        ("SINGLE_MONITOR", "Tek monitör", "Harici tek monitör", "monitor-dot"),
        ("DUAL_MONITOR", "Çift monitör", "İki harici monitör", "monitor"),
        ("DOCK", "Dizüstü dock istasyonu", "Tek kabloyla bağlantı", "plug"),
    ];

    private static readonly (string Key, string Name, string Icon)[] RequiredRoomFeatures =
    [
        ("SCREEN", "Ekran / projeksiyon", "projector"),
        ("VIDEO", "Video konferans", "video"),
        ("WHITEBOARD", "Beyaz tahta", "presentation"),
        ("DAYLIGHT", "Doğal ışık", "sun"),
    ];

    public static async Task<PalladiumResult> SeedAsync(Ofis360DbContext db, bool redrawSketch = false)
    {
        var report = new PalladiumReport();
        var b = PalladiumPlan.Building;
        var f = PalladiumPlan.Floor;

        // 1) Bina
        var building = await db.Buildings.FirstOrDefaultAsync(x => x.Code == b.Code);
        if (building is null)
        {
            building = new Building { Code = b.Code, Name = b.Name, Address = b.Address, Order = b.Order, Active = b.Active, IsDemo = b.IsDemo };
            db.Buildings.Add(building);
            await db.SaveChangesAsync();
            report.BuildingCreated = true;
        }

        // 2) Kat
        var floor = await db.Floors.FirstOrDefaultAsync(x => x.BuildingId == building.Id && x.Number == f.Number);
        if (floor is null)
        {
            floor = new Floor { BuildingId = building.Id, Number = f.Number, Name = f.Name, Description = f.Description, Reservable = f.Reservable, Active = f.Active, PlanWidth = f.PlanWidth, PlanHeight = f.PlanHeight };
            db.Floors.Add(floor);
            await db.SaveChangesAsync();
            report.FloorCreated = true;
        }

        // 3) Bolgeler
        var zoneIdByCode = new Dictionary<string, string>();
        foreach (var spec in PalladiumPlan.Zones)
        {
            var existing = await db.Zones.FirstOrDefaultAsync(z => z.FloorId == floor.Id && z.Code == spec.Code);
            if (existing is null)
            {
                var rect = PalladiumPlan.RectFromPx(spec.Px[0], spec.Px[1], spec.Px[2], spec.Px[3]);
                existing = new Zone { FloorId = floor.Id, Code = spec.Code, Name = spec.Name, Kind = spec.Kind, X = rect.X, Y = rect.Y, Width = rect.Width, Height = rect.Height, WindowSide = spec.WindowSide, Quiet = spec.Quiet, Reservable = spec.Reservable };
                db.Zones.Add(existing);
                await db.SaveChangesAsync();
                report.ZonesCreated++;
            }
            zoneIdByCode[spec.Code] = existing.Id;
        }

        // 4) Kroki ogeleri
        var elementCount = await db.FloorPlanElements.CountAsync(e => e.FloorId == floor.Id);
        if (elementCount > 0 && !redrawSketch)
        {
            report.ElementsSkipped = true;
        }
        else
        {
            if (elementCount > 0) await db.FloorPlanElements.Where(e => e.FloorId == floor.Id).ExecuteDeleteAsync();
            var rows = PalladiumPlan.Elements.Select((spec, index) =>
            {
                var rect = PalladiumPlan.RectFromPx(spec.Px[0], spec.Px[1], spec.Px[2], spec.Px[3]);
                var definition = PlanElementDefinitions.All[spec.Type];
                return new FloorPlanElement
                {
                    FloorId = floor.Id, Type = spec.Type, Label = spec.Label ?? definition.DefaultLabel,
                    X = rect.X, Y = rect.Y, Width = rect.Width, Height = rect.Height, Rotation = spec.Rotation ?? 0,
                    Style = spec.Style is null ? "{}" : JsonSerializer.Serialize(new { fill = spec.Style.Fill, stroke = spec.Style.Stroke, strokeWidth = spec.Style.StrokeWidth, fontSize = spec.Style.FontSize }, Json),
                    LayerOrder = spec.Layer ?? definition.BaseLayer * 10 + index % 10,
                    Locked = spec.Type is "WALL" or "WINDOW" or "TERRACE",
                };
            }).ToList();
            await SeedSupport.BulkInsertAsync(db, rows);
            report.ElementsCreated = rows.Count;
        }

        // 5) Masalar — ozellik katalogunda yalnizca eksik anahtarlar eklenir.
        foreach (var feature in RequiredDeskFeatures)
        {
            if (!await db.DeskFeatures.AnyAsync(x => x.Key == feature.Key))
            {
                db.DeskFeatures.Add(new DeskFeature { Key = feature.Key, Name = feature.Name, Description = feature.Description, Icon = feature.Icon });
            }
        }
        foreach (var feature in RequiredRoomFeatures)
        {
            if (!await db.RoomFeatures.AnyAsync(x => x.Key == feature.Key))
            {
                db.RoomFeatures.Add(new RoomFeature { Key = feature.Key, Name = feature.Name, Icon = feature.Icon });
            }
        }
        await db.SaveChangesAsync();

        var deskFeatureIdByKey = await db.DeskFeatures.ToDictionaryAsync(x => x.Key, x => x.Id);
        foreach (var spec in PalladiumPlan.Desks)
        {
            if (await db.Desks.AnyAsync(d => d.Code == spec.Code))
            {
                report.DesksExisting++;
                continue;
            }
            if (!zoneIdByCode.TryGetValue(spec.ZoneCode, out var zoneId)) throw new InvalidOperationException($"Bolge bulunamadi: {spec.ZoneCode}");
            var g = PalladiumPlan.CenterFromPx(spec.Cx, spec.Cy, spec.W, spec.H);
            var desk = new Desk
            {
                Code = spec.Code, FloorId = floor.Id, ZoneId = zoneId, X = g.X, Y = g.Y, Width = g.Width, Height = g.Height,
                Placed = true, Active = true, Popular = false, AccessibleOnly = false, Notes = spec.Notes,
            };
            foreach (var key in spec.Features.Where(deskFeatureIdByKey.ContainsKey))
            {
                desk.Features.Add(new DeskFeatureOnDesk { DeskId = desk.Id, FeatureId = deskFeatureIdByKey[key] });
            }
            db.Desks.Add(desk);
            await db.SaveChangesAsync();
            report.DesksCreated++;
        }

        // 6) Toplanti odalari
        var roomFeatureIdByKey = await db.RoomFeatures.ToDictionaryAsync(x => x.Key, x => x.Id);
        foreach (var spec in PalladiumPlan.Rooms)
        {
            if (await db.MeetingRooms.AnyAsync(r => r.Code == spec.Code))
            {
                report.RoomsExisting++;
                continue;
            }
            var rect = PalladiumPlan.RectFromPx(spec.Px[0], spec.Px[1], spec.Px[2], spec.Px[3]);
            var room = new MeetingRoom
            {
                Code = spec.Code, Name = spec.Name, FloorId = floor.Id, Capacity = spec.Capacity,
                X = rect.X, Y = rect.Y, Width = rect.Width, Height = rect.Height, Placed = true, Active = true,
                Notes = $"{spec.PlanLabel}. {spec.Notes ?? string.Empty}".Trim(),
            };
            foreach (var key in spec.Features.Where(roomFeatureIdByKey.ContainsKey))
            {
                room.Features.Add(new RoomFeatureOnRoom { RoomId = room.Id, FeatureId = roomFeatureIdByKey[key] });
            }
            db.MeetingRooms.Add(room);
            await db.SaveChangesAsync();
            report.RoomsCreated++;
        }

        return new PalladiumResult(building, floor, report);
    }

    public static void PrintReport(TextWriter log, PalladiumResult result)
    {
        var (building, floor, report) = result;
        log.WriteLine($"Bina: {building.Name} ({building.Code}) {(report.BuildingCreated ? "olusturuldu" : "mevcut")}");
        log.WriteLine($"Kat : {floor.Name} {(report.FloorCreated ? "olusturuldu" : "mevcut")} · plan {floor.PlanWidth}×{floor.PlanHeight}");
        log.WriteLine($"Bolge: +{report.ZonesCreated}");
        log.WriteLine($"Kroki: {(report.ElementsSkipped ? "mevcut kroki korundu (yeniden cizmek icin --redraw-sketch)" : $"+{report.ElementsCreated} oge")}");
        log.WriteLine($"Masa : +{report.DesksCreated} (mevcut {report.DesksExisting})");
        log.WriteLine($"Oda  : +{report.RoomsCreated} (mevcut {report.RoomsExisting})");
    }
}
