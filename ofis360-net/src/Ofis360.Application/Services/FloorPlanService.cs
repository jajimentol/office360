using Microsoft.EntityFrameworkCore;
using Ofis360.Application.Common;
using Ofis360.Application.Persistence;
using Ofis360.Application.Validation;
using Ofis360.Domain.Common;
using Ofis360.Domain.Entities;
using Ofis360.Domain.Plan;

namespace Ofis360.Application.Services;

public sealed record SnapshotFloor(string Id, string Name, int Number, int PlanWidth, int PlanHeight, bool Reservable);

public sealed record SnapshotDesk(
    string Id, string Code, int X, int Y, int Width, int Height, int Rotation, string ZoneId, string ZoneName, string FloorName,
    bool Active, bool Popular, bool AccessibleOnly, int ReservationCount);

public sealed record SnapshotRoom(string Id, string Code, string Name, int X, int Y, int Width, int Height, int Capacity, bool Active, int ReservationCount);

public sealed record SnapshotZone(string Id, string Name, bool Reservable);

public sealed record UnplacedDesk(string Id, string Code, int Width, int Height, string ZoneId, string ZoneName, bool Active, bool Popular, bool AccessibleOnly);

public sealed record UnplacedRoom(string Id, string Code, string Name, int Width, int Height, int Capacity, bool Active);

/// <summary>Editorun ihtiyac duydugu tum kat verisi.</summary>
public sealed record FloorPlanSnapshot(
    SnapshotFloor Floor, IReadOnlyList<PlanElement> Elements, IReadOnlyList<SnapshotDesk> Desks, IReadOnlyList<SnapshotRoom> Rooms,
    IReadOnlyList<SnapshotZone> Zones, IReadOnlyList<UnplacedDesk> UnplacedDesks, IReadOnlyList<UnplacedRoom> UnplacedRooms);

public sealed record SavePlanResult(IReadOnlyList<PlanElement> Elements, int CreatedCount, int UpdatedCount, int DeletedCount, int MovedDeskCount, int MovedRoomCount);

public sealed record AutoPlaceResult(int PlacedDesks, int RemainingDesks, int PlacedRooms, int RemainingRooms);

/// <summary>
/// Kat krokisi servisi. Kroki yalnizca fiziksel yapiyi tanimlar; masa/oda konumu kendi tablolarinda tutulur.
/// Kaydetme tek transaction'da (atomik) yapilir.
/// </summary>
public sealed class FloorPlanService(IOfis360Db db, AuditService audit)
{
    private readonly record struct Box(double X, double Y, double Width, double Height);

    public async Task<List<PlanElement>> GetPlanElementsAsync(string floorId, CancellationToken ct = default)
    {
        var rows = await db.FloorPlanElements.AsNoTracking()
            .Where(e => e.FloorId == floorId)
            .OrderBy(e => e.LayerOrder).ThenBy(e => e.CreatedAt)
            .ToListAsync(ct);
        return rows.Select(AvailabilityService.ToPlanElement).ToList();
    }

    public async Task<FloorPlanSnapshot> GetSnapshotAsync(string floorId, CancellationToken ct = default)
    {
        var floor = await db.Floors.AsNoTracking()
            .Include(f => f.Elements.OrderBy(e => e.LayerOrder).ThenBy(e => e.CreatedAt))
            .Include(f => f.Zones.OrderBy(z => z.Code))
            .Include(f => f.Desks.OrderBy(d => d.Code)).ThenInclude(d => d.Zone)
            .Include(f => f.Rooms.OrderBy(r => r.Code))
            .FirstOrDefaultAsync(f => f.Id == floorId, ct)
            ?? throw AppErrors.NotFound("Kat bulunamadı.");

        var deskCounts = await db.DeskReservations.Where(r => r.Desk.FloorId == floorId)
            .GroupBy(r => r.DeskId).Select(g => new { DeskId = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.DeskId, x => x.Count, ct);
        var roomCounts = await db.RoomReservations.Where(r => r.Room.FloorId == floorId)
            .GroupBy(r => r.RoomId).Select(g => new { RoomId = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.RoomId, x => x.Count, ct);

        return new FloorPlanSnapshot(
            new SnapshotFloor(floor.Id, floor.Name, floor.Number, floor.PlanWidth, floor.PlanHeight, floor.Reservable),
            floor.Elements.Select(AvailabilityService.ToPlanElement).ToList(),
            floor.Desks.Where(d => d.Placed).Select(d => new SnapshotDesk(d.Id, d.Code, d.X, d.Y, d.Width, d.Height, d.Rotation, d.ZoneId, d.Zone.Name,
                floor.Name, d.Active, d.Popular, d.AccessibleOnly, deskCounts.GetValueOrDefault(d.Id))).ToList(),
            floor.Rooms.Where(r => r.Placed).Select(r => new SnapshotRoom(r.Id, r.Code, r.Name, r.X, r.Y, r.Width, r.Height, r.Capacity, r.Active,
                roomCounts.GetValueOrDefault(r.Id))).ToList(),
            floor.Zones.Select(z => new SnapshotZone(z.Id, z.Name, z.Reservable)).ToList(),
            floor.Desks.Where(d => !d.Placed).Select(d => new UnplacedDesk(d.Id, d.Code, d.Width, d.Height, d.ZoneId, d.Zone.Name, d.Active, d.Popular, d.AccessibleOnly)).ToList(),
            floor.Rooms.Where(r => !r.Placed && r.Active).Select(r => new UnplacedRoom(r.Id, r.Code, r.Name, r.Width, r.Height, r.Capacity, r.Active)).ToList());
    }

    /// <summary>Plan sinirlari disina tasan ogeleri reddeder.</summary>
    private static void AssertWithinPlan(string label, Box rect, int planWidth, int planHeight)
    {
        if (rect.X < 0 || rect.Y < 0 || rect.X + rect.Width > planWidth || rect.Y + rect.Height > planHeight)
        {
            throw AppErrors.Validation($"\"{label}\" öğesi kat planı sınırlarının ({planWidth} × {planHeight}) dışında kalıyor.");
        }
    }

    /// <summary>Krokiyi ve rezervasyon nesnelerinin konumlarini atomik olarak kaydeder; rezervasyonlara dokunulmaz.</summary>
    public async Task<SavePlanResult> SaveAsync(SavePlanInput input, AuditActor actor, CancellationToken ct = default)
    {
        var floor = await db.Floors.AsNoTracking().Where(f => f.Id == input.FloorId)
            .Select(f => new { f.Id, f.Name, f.PlanWidth, f.PlanHeight }).FirstOrDefaultAsync(ct)
            ?? throw AppErrors.NotFound("Kat bulunamadı.");

        // --- Kroki ogeleri dogrulamasi ---
        foreach (var element in input.Elements)
        {
            if (!PlanElementDefinitions.All.TryGetValue(element.Type, out var definition))
            {
                throw AppErrors.Validation($"Desteklenmeyen öğe türü: {element.Type}");
            }
            var label = string.IsNullOrWhiteSpace(element.Label) ? definition.Label : element.Label.Trim();
            AssertWithinPlan(label, new Box(element.X, element.Y, element.Width, element.Height), floor.PlanWidth, floor.PlanHeight);

            if (element.LinkedResourceType is not null && string.IsNullOrEmpty(element.LinkedResourceId))
            {
                throw AppErrors.Validation($"\"{label}\" öğesi için bağlı kaynak kimliği eksik.");
            }
            if (!string.IsNullOrEmpty(element.LinkedResourceId) && element.LinkedResourceType is null)
            {
                throw AppErrors.Validation($"\"{label}\" öğesi için bağlı kaynak türü eksik.");
            }
        }

        // --- Bagli kaynaklarin bu kata ait oldugunu dogrula ---
        var existingIds = (await db.FloorPlanElements.Where(e => e.FloorId == floor.Id).Select(e => e.Id).ToListAsync(ct)).ToHashSet();
        var floorDesks = await db.Desks.AsNoTracking().Where(d => d.FloorId == floor.Id).Select(d => new { d.Id, d.Code }).ToListAsync(ct);
        var floorRooms = await db.MeetingRooms.AsNoTracking().Where(r => r.FloorId == floor.Id).Select(r => new { r.Id, r.Name }).ToListAsync(ct);
        var deskLabel = floorDesks.ToDictionary(d => d.Id, d => d.Code);
        var roomLabel = floorRooms.ToDictionary(r => r.Id, r => r.Name);

        foreach (var element in input.Elements.Where(e => e.LinkedResourceType == LinkedResourceTypes.Desk && !string.IsNullOrEmpty(e.LinkedResourceId)))
        {
            if (!deskLabel.ContainsKey(element.LinkedResourceId!)) throw AppErrors.Validation("Bağlanmak istenen masa bu kata ait değil.");
        }
        foreach (var element in input.Elements.Where(e => e.LinkedResourceType == LinkedResourceTypes.MeetingRoom && !string.IsNullOrEmpty(e.LinkedResourceId)))
        {
            if (!roomLabel.ContainsKey(element.LinkedResourceId!)) throw AppErrors.Validation("Bağlanmak istenen toplantı odası bu kata ait değil.");
        }

        // --- Masa ve oda konumlari dogrulamasi ---
        var deskPlacements = input.DeskPlacementsOrEmpty;
        var roomPlacements = input.RoomPlacementsOrEmpty;
        foreach (var placement in deskPlacements)
        {
            if (!deskLabel.ContainsKey(placement.Id)) throw AppErrors.Validation("Taşınmak istenen masa bu kata ait değil.");
            AssertWithinPlan(deskLabel[placement.Id],
                new Box(placement.X - placement.Width / 2.0, placement.Y - placement.Height / 2.0, placement.Width, placement.Height),
                floor.PlanWidth, floor.PlanHeight);
        }
        foreach (var placement in roomPlacements)
        {
            if (!roomLabel.ContainsKey(placement.Id)) throw AppErrors.Validation("Taşınmak istenen toplantı odası bu kata ait değil.");
            AssertWithinPlan(roomLabel[placement.Id], new Box(placement.X, placement.Y, placement.Width, placement.Height), floor.PlanWidth, floor.PlanHeight);
        }

        // --- Atomik kayit ---
        var keptIds = input.Elements.Select(e => e.Id).Where(existingIds.Contains).ToHashSet();
        var removedIds = existingIds.Where(id => !keptIds.Contains(id)).ToList();
        var toUpdate = input.Elements.Where(e => existingIds.Contains(e.Id)).ToList();
        var toCreate = input.Elements.Where(e => !existingIds.Contains(e.Id)).ToList();

        await db.InTransactionAsync(async token =>
        {
            if (removedIds.Count > 0)
            {
                await db.FloorPlanElements.Where(e => removedIds.Contains(e.Id) && e.FloorId == floor.Id).ExecuteDeleteAsync(token);
            }

            if (toUpdate.Count > 0)
            {
                var updateIds = toUpdate.Select(e => e.Id).ToList();
                var rows = await db.FloorPlanElements.Where(e => updateIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, token);
                foreach (var element in toUpdate) Apply(rows[element.Id], element);
            }

            foreach (var element in toCreate)
            {
                var row = new FloorPlanElement { FloorId = floor.Id };
                Apply(row, element);
                db.FloorPlanElements.Add(row);
            }

            // Masa ve oda konumlari: yalnizca koordinat/olcu guncellenir; konum verilen nesne yerlesik sayilir.
            if (deskPlacements.Count > 0)
            {
                var ids = deskPlacements.Select(p => p.Id).ToList();
                var desks = await db.Desks.Where(d => ids.Contains(d.Id)).ToDictionaryAsync(d => d.Id, token);
                foreach (var p in deskPlacements)
                {
                    var desk = desks[p.Id];
                    desk.X = p.X; desk.Y = p.Y; desk.Width = p.Width; desk.Height = p.Height; desk.Rotation = p.Rotation; desk.Placed = true;
                }
            }
            if (roomPlacements.Count > 0)
            {
                var ids = roomPlacements.Select(p => p.Id).ToList();
                var rooms = await db.MeetingRooms.Where(r => ids.Contains(r.Id)).ToDictionaryAsync(r => r.Id, token);
                foreach (var p in roomPlacements)
                {
                    var room = rooms[p.Id];
                    room.X = p.X; room.Y = p.Y; room.Width = p.Width; room.Height = p.Height; room.Placed = true;
                }
            }

            await db.SaveChangesAsync(token);
            return true;
        }, ct);

        await audit.RecordAsync(actor, AuditActions.FloorPlanSave, "FloorPlanElement", floor.Id,
            $"{floor.Name} krokisi güncellendi: {toCreate.Count} öğe eklendi, {toUpdate.Count} öğe düzenlendi, {removedIds.Count} öğe silindi.",
            new { floorId = floor.Id, created = toCreate.Count, updated = toUpdate.Count, deleted = removedIds.Count, movedDesks = deskPlacements.Count, movedRooms = roomPlacements.Count }, ct);

        return new SavePlanResult(await GetPlanElementsAsync(floor.Id, ct), toCreate.Count, toUpdate.Count, removedIds.Count, deskPlacements.Count, roomPlacements.Count);
    }

    private static void Apply(FloorPlanElement row, PlanElementInput e)
    {
        row.Type = e.Type;
        row.Label = string.IsNullOrWhiteSpace(e.Label) ? null : e.Label.Trim();
        row.X = e.X; row.Y = e.Y; row.Width = e.Width; row.Height = e.Height; row.Rotation = e.Rotation;
        row.Style = PlanGeometry.SerializeStyle(e.Style is null ? PlanElementStyle.Empty : new PlanElementStyle(e.Style.Fill, e.Style.Stroke, e.Style.StrokeWidth, e.Style.FontSize));
        row.LayerOrder = e.LayerOrder; row.Locked = e.Locked; row.Visible = e.Visible;
        row.LinkedResourceId = string.IsNullOrEmpty(e.LinkedResourceId) ? null : e.LinkedResourceId;
        row.LinkedResourceType = e.LinkedResourceType;
    }

    private static bool Overlaps(Box a, Box b, double gap) =>
        a.X < b.X + b.Width + gap && a.X + a.Width + gap > b.X && a.Y < b.Y + b.Height + gap && a.Y + a.Height + gap > b.Y;

    private static readonly HashSet<string> BlockedElementTypes =
        ["WALL", "CORRIDOR", "ELEVATOR", "STAIRS", "WC", "KITCHEN", "RESTRICTED", "TERRACE", "DOOR", "WINDOW"];

    /// <summary>
    /// Havuzdaki masa ve odalari krokinin BOS alanlarina yerlestirir. Mevcut nesneler tasinmaz; yer bulunamayan
    /// nesneler havuzda kalir. Yerlestirme deterministiktir (margin 18, gap 10, step 10).
    /// </summary>
    public async Task<AutoPlaceResult> AutoPlacePoolItemsAsync(string floorId, AuditActor actor, CancellationToken ct = default)
    {
        var floor = await db.Floors
            .Include(f => f.Elements).Include(f => f.Zones).Include(f => f.Desks).Include(f => f.Rooms)
            .FirstOrDefaultAsync(f => f.Id == floorId, ct)
            ?? throw AppErrors.NotFound("Kat bulunamadı.");

        const int margin = 18;
        const int gap = 10;
        var planWidth = floor.PlanWidth;
        var planHeight = floor.PlanHeight;

        var occupied = new List<Box>();
        occupied.AddRange(floor.Elements.Where(e => BlockedElementTypes.Contains(e.Type)).Select(e => new Box(e.X, e.Y, e.Width, e.Height)));
        occupied.AddRange(floor.Desks.Where(d => d.Placed).Select(d => new Box(d.X - d.Width / 2.0, d.Y - d.Height / 2.0, d.Width, d.Height)));
        occupied.AddRange(floor.Rooms.Where(r => r.Placed).Select(r => new Box(r.X, r.Y, r.Width, r.Height)));

        bool Fits(Box box) =>
            box.X >= margin && box.Y >= margin && box.X + box.Width <= planWidth - margin && box.Y + box.Height <= planHeight - margin
            && !occupied.Any(item => Overlaps(box, item, gap));

        Box? FindSlot(int width, int height, Box? within)
        {
            const int step = 10;
            var areas = new List<(double X0, double Y0, double X1, double Y1)>();
            if (within is Box w)
            {
                areas.Add((Math.Max(margin, w.X + 6), Math.Max(margin, w.Y + 6), Math.Min(planWidth - margin, w.X + w.Width - 6), Math.Min(planHeight - margin, w.Y + w.Height - 6)));
            }
            areas.Add((margin, margin, planWidth - margin, planHeight - margin));

            foreach (var area in areas)
            {
                for (var y = area.Y0; y + height <= area.Y1; y += step)
                {
                    for (var x = area.X0; x + width <= area.X1; x += step)
                    {
                        var box = new Box(x, y, width, height);
                        if (Fits(box)) return box;
                    }
                }
            }
            return null;
        }

        var poolDesks = floor.Desks.Where(d => !d.Placed).OrderBy(d => d.Code, StringComparer.Ordinal).ToList();
        // Pasif oda otomatik yerlestirmeyle krokiye geri donmemeli.
        var poolRooms = floor.Rooms.Where(r => !r.Placed && r.Active).OrderBy(r => r.Code, StringComparer.Ordinal).ToList();
        var deskUpdates = new List<(Desk Desk, int X, int Y)>();
        var roomUpdates = new List<(MeetingRoom Room, int X, int Y)>();

        // Odalar once yerlestirilir: daha buyuk olduklari icin yer bulmalari daha zordur.
        var meetingZones = floor.Zones.Where(z => z.Kind == ZoneKinds.Meeting).ToList();
        for (var index = 0; index < poolRooms.Count; index++)
        {
            var room = poolRooms[index];
            var preferred = meetingZones.Count > 0 ? meetingZones[index % meetingZones.Count] : null;
            var slot = FindSlot(room.Width, room.Height, preferred is null ? null : new Box(preferred.X, preferred.Y, preferred.Width, preferred.Height));
            if (slot is not Box s) continue;
            occupied.Add(s);
            roomUpdates.Add((room, (int)s.X, (int)s.Y));
        }

        var zoneById = floor.Zones.ToDictionary(z => z.Id);
        foreach (var desk in poolDesks)
        {
            var zone = zoneById.GetValueOrDefault(desk.ZoneId);
            var slot = FindSlot(desk.Width, desk.Height, zone is null ? null : new Box(zone.X, zone.Y, zone.Width, zone.Height));
            if (slot is not Box s) continue;
            occupied.Add(s);
            // Masa koordinati merkez noktasidir.
            deskUpdates.Add((desk, (int)PlanGeometry.JsRound(s.X + desk.Width / 2.0), (int)PlanGeometry.JsRound(s.Y + desk.Height / 2.0)));
        }

        if (deskUpdates.Count == 0 && roomUpdates.Count == 0)
        {
            return new AutoPlaceResult(0, poolDesks.Count, 0, poolRooms.Count);
        }

        await db.InTransactionAsync(async token =>
        {
            foreach (var (room, x, y) in roomUpdates) { room.X = x; room.Y = y; room.Placed = true; }
            foreach (var (desk, x, y) in deskUpdates) { desk.X = x; desk.Y = y; desk.Placed = true; }
            await db.SaveChangesAsync(token);
            return true;
        }, ct);

        await audit.RecordAsync(actor, AuditActions.FloorPlanSave, "FloorPlanElement", floor.Id,
            $"{floor.Name} krokisine havuzdan {deskUpdates.Count} masa ve {roomUpdates.Count} toplantı odası yerleştirildi.",
            new { floorId = floor.Id, autoPlacedDesks = deskUpdates.Count, autoPlacedRooms = roomUpdates.Count,
                remainingDesks = poolDesks.Count - deskUpdates.Count, remainingRooms = poolRooms.Count - roomUpdates.Count }, ct);

        return new AutoPlaceResult(deskUpdates.Count, poolDesks.Count - deskUpdates.Count, roomUpdates.Count, poolRooms.Count - roomUpdates.Count);
    }

    /// <summary>Baska bir katin krokisini bu kata kopyalar (sablon); kimlikler gecici olarak yeniden uretilir.</summary>
    public async Task<List<PlanElement>> CopyPlanFromFloorAsync(string sourceFloorId, CancellationToken ct = default)
    {
        var elements = await GetPlanElementsAsync(sourceFloorId, ct);
        return elements.Select((e, index) => e with { Id = $"kopya-{index}-{e.Type}" }).ToList();
    }

    /// <summary>Editorden hizli masa olusturma (kaynak: floor-plan.actions createDeskFromPlanAction).</summary>
    public async Task<SnapshotDesk> CreateDeskFromPlanAsync(QuickDeskInput input, AuditActor actor, CancellationToken ct = default)
    {
        var code = input.Code.ToUpperInvariant();
        var floor = await db.Floors.AsNoTracking().FirstOrDefaultAsync(f => f.Id == input.FloorId, ct);
        var zone = await db.Zones.AsNoTracking().FirstOrDefaultAsync(z => z.Id == input.ZoneId, ct);
        var exists = await db.Desks.AnyAsync(d => d.Code == code, ct);

        if (floor is null) throw AppErrors.NotFound("Kat bulunamadı.");
        if (zone is null) throw AppErrors.NotFound("Bölge bulunamadı.");
        if (zone.FloorId != floor.Id) throw AppErrors.Validation("Seçilen bölge bu kata ait değil.");
        if (exists) throw AppErrors.Conflict("Bu masa kodu zaten kullanılıyor.");

        var halfW = PlanGeometry.DeskSize.Width / 2.0;
        var halfH = PlanGeometry.DeskSize.Height / 2.0;
        if (input.X - halfW < 0 || input.Y - halfH < 0 || input.X + halfW > floor.PlanWidth || input.Y + halfH > floor.PlanHeight)
        {
            throw AppErrors.Validation("Masa kat planı sınırlarının dışında.");
        }

        var desk = new Desk { Code = code, FloorId = floor.Id, ZoneId = zone.Id, X = input.X, Y = input.Y, Popular = input.Popular, AccessibleOnly = input.AccessibleOnly };
        db.Desks.Add(desk);
        await db.SaveChangesAsync(ct);

        await audit.RecordAsync(actor, AuditActions.DeskCreate, "Desk", desk.Id,
            $"{desk.Code} masası kat planı editöründen {floor.Name} katına eklendi.", new { code = desk.Code, source = "PLAN_EDITOR" }, ct);

        return new SnapshotDesk(desk.Id, desk.Code, desk.X, desk.Y, desk.Width, desk.Height, desk.Rotation, desk.ZoneId, zone.Name, floor.Name,
            desk.Active, desk.Popular, desk.AccessibleOnly, 0);
    }

    /// <summary>Editorden hizli toplanti odasi olusturma.</summary>
    public async Task<SnapshotRoom> CreateRoomFromPlanAsync(QuickRoomInput input, AuditActor actor, CancellationToken ct = default)
    {
        var code = input.Code.ToUpperInvariant();
        var floor = await db.Floors.AsNoTracking().FirstOrDefaultAsync(f => f.Id == input.FloorId, ct);
        var exists = await db.MeetingRooms.AnyAsync(r => r.Code == code, ct);

        if (floor is null) throw AppErrors.NotFound("Kat bulunamadı.");
        if (exists) throw AppErrors.Conflict("Bu oda kodu zaten kullanılıyor.");
        if (input.X < 0 || input.Y < 0 || input.X + input.Width > floor.PlanWidth || input.Y + input.Height > floor.PlanHeight)
        {
            throw AppErrors.Validation("Toplantı odası kat planı sınırlarının dışında.");
        }

        var room = new MeetingRoom { Code = code, Name = input.Name, FloorId = floor.Id, Capacity = input.Capacity, X = input.X, Y = input.Y, Width = input.Width, Height = input.Height };
        db.MeetingRooms.Add(room);
        await db.SaveChangesAsync(ct);

        await audit.RecordAsync(actor, AuditActions.RoomCreate, "MeetingRoom", room.Id,
            $"{room.Name} ({room.Code}) toplantı odası kat planı editöründen eklendi.", new { code = room.Code, source = "PLAN_EDITOR" }, ct);

        return new SnapshotRoom(room.Id, room.Code, room.Name, room.X, room.Y, room.Width, room.Height, room.Capacity, room.Active, 0);
    }
}
