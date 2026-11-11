using Microsoft.EntityFrameworkCore;
using Ofis360.Application.Common;
using Ofis360.Application.Persistence;
using Ofis360.Application.Validation;
using Ofis360.Domain.Common;
using Ofis360.Domain.Entities;

namespace Ofis360.Application.Services;

public sealed record BuildingListItem(Building Building, int FloorCount);
public sealed record FloorListItem(Floor Floor, int DeskCount, int RoomCount);
public sealed record DeskListItem(Desk Desk, int ReservationCount);
public sealed record RoomListItem(MeetingRoom Room, int ReservationCount);

public sealed record DeskListFilters(
    string? FloorId = null, string? Search = null, bool OnlyInactive = false, string? Placement = null, int? Take = null, int? Skip = null);

public sealed record FloorCapacityStats(
    string FloorId, int FloorNumber, string FloorName, string BuildingId, string BuildingName, bool BuildingActive, bool Reservable,
    int TotalDesks, int PlacedDesks, int UnplacedDesks, int ActiveDesks, int ClosedDesks, int TotalRooms, int PlacedRooms, int UnplacedRooms);

/// <summary>
/// Bina, kat, bolge, masa ve toplanti odasi yonetimi (admin). Silme islemlerinde gecmis rezervasyon varsa
/// pasiflestirme onerilir; raporlardaki gecmis veri bozulmaz.
/// </summary>
public sealed class SpaceService(IOfis360Db db, AuditService audit, NotificationService notifications, IClock clock)
{
    private readonly ResourceReservationCanceller canceller = new(db, notifications, clock);

    private static string CancelledNote(int count) => count > 0 ? $" {count} gelecek rezervasyon iptal edildi." : "";

    // --- Binalar ---

    public Task<List<BuildingListItem>> ListBuildingsAsync(bool includeInactive = false, CancellationToken ct = default)
    {
        var q = db.Buildings.AsNoTracking().AsQueryable();
        if (!includeInactive) q = q.Where(b => b.Active);
        return q.OrderBy(b => b.Order).ThenBy(b => b.Name)
            .Select(b => new BuildingListItem(b, b.Floors.Count))
            .ToListAsync(ct);
    }

    public async Task<Building> CreateBuildingAsync(BuildingInput input, AuditActor actor, CancellationToken ct = default)
    {
        if (await db.Buildings.AnyAsync(b => b.Code == input.Code, ct)) throw AppErrors.Conflict("Bu bina kodu zaten kullanılıyor.");

        var building = new Building { Code = input.Code, Name = input.Name, Address = input.Address, Order = input.Order, Active = input.Active };
        db.Buildings.Add(building);
        await db.SaveChangesAsync(ct);

        await audit.RecordAsync(actor, AuditActions.BuildingCreate, "Building", building.Id,
            $"{building.Name} binası eklendi.", new { code = building.Code }, ct);
        return building;
    }

    public async Task<Building> UpdateBuildingAsync(string buildingId, BuildingUpdateInput input, AuditActor actor, CancellationToken ct = default)
    {
        var building = await db.Buildings.FirstOrDefaultAsync(b => b.Id == buildingId, ct) ?? throw AppErrors.NotFound("Bina bulunamadı.");

        if (input.Code is not null && input.Code != building.Code && await db.Buildings.AnyAsync(b => b.Code == input.Code, ct))
        {
            throw AppErrors.Conflict("Bu bina kodu zaten kullanılıyor.");
        }

        if (input.Code is not null) building.Code = input.Code;
        if (input.Name is not null) building.Name = input.Name;
        if (input.Address is not null) building.Address = input.Address;
        if (input.Order is int order) building.Order = order;
        var closing = building.Active && input.Active == false;
        if (input.Active is bool active) building.Active = active;
        var cancelled = await db.InTransactionAsync(async token =>
        {
            await db.SaveChangesAsync(token);
            if (!closing) return 0;
            var notice = $"{building.Name} binası kullanıma kapatıldı";
            return await canceller.CancelDesksAsync(r => r.Desk.Floor.BuildingId == buildingId, null, null, notice + ".", notice, actor, token)
                + await canceller.CancelRoomsAsync(r => r.Room.Floor.BuildingId == buildingId, null, null, notice + ".", notice, actor, token);
        }, ct);

        await audit.RecordAsync(actor, AuditActions.BuildingUpdate, "Building", buildingId,
            $"{building.Name} binası güncellendi.{(input.Active == false ? " Bina çalışan erişimine kapatıldı." : "")}{CancelledNote(cancelled)}", input, ct);
        return building;
    }

    // --- Katlar ---

    /// <summary>Katlar bina sirasi + kat numarasina gore; her kat binasiyla birlikte doner.</summary>
    public Task<List<FloorListItem>> ListFloorsAsync(string? buildingId = null, bool onlyActiveBuildings = false, CancellationToken ct = default)
    {
        var q = db.Floors.AsNoTracking().AsQueryable();
        if (buildingId is not null) q = q.Where(f => f.BuildingId == buildingId);
        if (onlyActiveBuildings) q = q.Where(f => f.Building.Active);
        return q.Include(f => f.Building).Include(f => f.Zones.OrderBy(z => z.Code))
            .OrderBy(f => f.Building.Order).ThenBy(f => f.Number)
            .Select(f => new FloorListItem(f, f.Desks.Count, f.Rooms.Count))
            .ToListAsync(ct);
    }

    /// <summary>"Palladium Tower · 36. Kat" bicimindeki tam kat etiketi.</summary>
    public static string FloorLabel(Floor floor) => floor.Building is not null ? $"{floor.Building.Name} · {floor.Name}" : floor.Name;

    public async Task<Floor> GetFloorDetailAsync(string floorId, CancellationToken ct = default) =>
        await db.Floors.AsNoTracking()
            .Include(f => f.Building)
            .Include(f => f.Zones.OrderBy(z => z.Code))
            .Include(f => f.Desks.OrderBy(d => d.Code)).ThenInclude(d => d.Zone)
            .Include(f => f.Desks.OrderBy(d => d.Code)).ThenInclude(d => d.Features).ThenInclude(l => l.Feature)
            .Include(f => f.Rooms.OrderBy(r => r.Code)).ThenInclude(r => r.Features).ThenInclude(l => l.Feature)
            .FirstOrDefaultAsync(f => f.Id == floorId, ct)
        ?? throw AppErrors.NotFound("Kat bulunamadı.");

    public async Task<Floor> CreateFloorAsync(FloorInput input, AuditActor actor, CancellationToken ct = default)
    {
        var building = await db.Buildings.AsNoTracking().FirstOrDefaultAsync(b => b.Id == input.BuildingId, ct)
            ?? throw AppErrors.NotFound("Bina bulunamadı.");
        if (await db.Floors.AnyAsync(f => f.BuildingId == input.BuildingId && f.Number == input.Number, ct))
        {
            throw AppErrors.Conflict("Bu binada bu kat numarası zaten tanımlı.");
        }

        // K3: kaynak servisteki fiili varsayilan 1040x660 korunur.
        var floor = new Floor
        {
            BuildingId = input.BuildingId, Number = input.Number, Name = input.Name, Description = input.Description,
            Reservable = input.Reservable, Active = input.Active,
            PlanWidth = input.PlanWidth ?? 1040, PlanHeight = input.PlanHeight ?? 660,
        };
        floor.Zones.Add(new Zone { Code = "A", Name = "A Bölgesi – Çalışma Alanı", Kind = ZoneKinds.Work, X = 80, Y = 80, Width = 400, Height = 240, Reservable = true });
        floor.Zones.Add(new Zone { Code = "KOR", Name = "Koridor", Kind = ZoneKinds.Corridor, X = 80, Y = 360, Width = 880, Height = 56, Reservable = false });
        db.Floors.Add(floor);
        await db.SaveChangesAsync(ct);

        await audit.RecordAsync(actor, AuditActions.FloorCreate, "Floor", floor.Id,
            $"{building.Name} · {floor.Name} eklendi.", new { number = floor.Number, buildingId = building.Id }, ct);
        return floor;
    }

    public async Task<Floor> UpdateFloorAsync(string floorId, FloorUpdateInput input, AuditActor actor, CancellationToken ct = default)
    {
        var floor = await db.Floors.FirstOrDefaultAsync(f => f.Id == floorId, ct) ?? throw AppErrors.NotFound("Kat bulunamadı.");

        var targetBuildingId = input.BuildingId ?? floor.BuildingId;
        var targetNumber = input.Number ?? floor.Number;
        if (targetBuildingId != floor.BuildingId || targetNumber != floor.Number)
        {
            if (targetBuildingId != floor.BuildingId && !await db.Buildings.AnyAsync(b => b.Id == targetBuildingId, ct))
            {
                throw AppErrors.NotFound("Bina bulunamadı.");
            }
            var clashId = await db.Floors.Where(f => f.BuildingId == targetBuildingId && f.Number == targetNumber).Select(f => f.Id).FirstOrDefaultAsync(ct);
            if (clashId is not null && clashId != floorId) throw AppErrors.Conflict("Bu binada bu kat numarası zaten tanımlı.");
        }

        if (input.BuildingId is not null) floor.BuildingId = input.BuildingId;
        if (input.Number is int number) floor.Number = number;
        if (input.Name is not null) floor.Name = input.Name;
        if (input.Description is not null) floor.Description = input.Description;
        var closing = (floor.Reservable && input.Reservable == false) || (floor.Active && input.Active == false);
        if (input.Reservable is bool reservable) floor.Reservable = reservable;
        if (input.Active is bool active) floor.Active = active;
        if (input.PlanWidth is int w) floor.PlanWidth = w;
        if (input.PlanHeight is int h) floor.PlanHeight = h;
        var cancelled = await db.InTransactionAsync(async token =>
        {
            await db.SaveChangesAsync(token);
            if (!closing) return 0;
            var notice = $"{floor.Name} kullanıma kapatıldı";
            return await canceller.CancelDesksAsync(r => r.Desk.FloorId == floorId, null, null, notice + ".", notice, actor, token)
                + await canceller.CancelRoomsAsync(r => r.Room.FloorId == floorId, null, null, notice + ".", notice, actor, token);
        }, ct);

        await audit.RecordAsync(actor, AuditActions.FloorUpdate, "Floor", floorId, $"{floor.Name} güncellendi.{CancelledNote(cancelled)}", input, ct);
        return floor;
    }

    public async Task<Zone> UpdateZoneAsync(string zoneId, ZoneUpdateInput input, AuditActor actor, CancellationToken ct = default)
    {
        var zone = await db.Zones.Include(z => z.Floor).FirstOrDefaultAsync(z => z.Id == zoneId, ct) ?? throw AppErrors.NotFound("Bölge bulunamadı.");

        if (input.Name is not null) zone.Name = input.Name;
        if (input.Kind is not null) zone.Kind = input.Kind;
        if (input.X is int x) zone.X = x;
        if (input.Y is int y) zone.Y = y;
        if (input.Width is int w) zone.Width = w;
        if (input.Height is int h) zone.Height = h;
        if (input.WindowSide is bool ws) zone.WindowSide = ws;
        if (input.Quiet is bool q) zone.Quiet = q;
        var closing = zone.Reservable && input.Reservable == false;
        if (input.Reservable is bool reservable) zone.Reservable = reservable;
        var cancelled = await db.InTransactionAsync(async token =>
        {
            await db.SaveChangesAsync(token);
            if (!closing) return 0;
            var notice = $"{zone.Name} bölgesi rezervasyona kapatıldı";
            return await canceller.CancelDesksAsync(res => res.Desk.ZoneId == zoneId, null, null, notice + ".", notice, actor, token);
        }, ct);

        await audit.RecordAsync(actor, AuditActions.ZoneUpdate, "Zone", zoneId,
            $"{zone.Floor.Name} · {zone.Name} bölgesi güncellendi.{CancelledNote(cancelled)}", input, ct);
        return zone;
    }

    // --- Masalar ---

    private IQueryable<Desk> DeskQuery(DeskListFilters filters)
    {
        var q = db.Desks.AsNoTracking().AsQueryable();
        if (filters.FloorId is not null) q = q.Where(d => d.FloorId == filters.FloorId);
        if (filters.OnlyInactive) q = q.Where(d => !d.Active);
        if (filters.Placement is not null) { var placed = filters.Placement == "placed"; q = q.Where(d => d.Placed == placed); }
        if (!string.IsNullOrEmpty(filters.Search)) { var pattern = Search.Pattern(filters.Search); q = q.Where(d => EF.Functions.Like(d.Code.ToUpper().Replace("İ", "I"), pattern, Search.Escape)); }
        return q;
    }

    /// <summary>Admin masa listesi; 150+ masa olcegi icin sayfalama destekler, toplam sayi DB'de hesaplanir.</summary>
    public async Task<PagedResult<DeskListItem>> ListDesksAsync(DeskListFilters filters, CancellationToken ct = default)
    {
        var take = Paging.Take(filters.Take, 100);
        var skip = Paging.Skip(filters.Skip);
        var q = DeskQuery(filters);
        var total = await q.CountAsync(ct);
        var items = await q
            .Include(d => d.Floor).ThenInclude(f => f.Building)
            .Include(d => d.Zone)
            .Include(d => d.Features).ThenInclude(l => l.Feature)
            .OrderBy(d => d.Floor.Building.Order).ThenBy(d => d.Floor.Number).ThenBy(d => d.Code)
            .Skip(skip).Take(take)
            .Select(d => new DeskListItem(d, d.Reservations.Count))
            .ToListAsync(ct);
        return new PagedResult<DeskListItem>(items, total, take, skip);
    }

    /// <summary>Kat bazinda masa ve oda kapasite sayaclari; sayimlar veritabaninda yapilir.</summary>
    public async Task<List<FloorCapacityStats>> GetFloorCapacityStatsAsync(CancellationToken ct = default)
    {
        var floors = await db.Floors.AsNoTracking().Include(f => f.Building)
            .OrderBy(f => f.Building.Order).ThenBy(f => f.Number).ToListAsync(ct);
        var deskGroups = await db.Desks.GroupBy(d => new { d.FloorId, d.Placed, d.Active })
            .Select(g => new { g.Key.FloorId, g.Key.Placed, g.Key.Active, Count = g.Count() }).ToListAsync(ct);
        var roomGroups = await db.MeetingRooms.GroupBy(r => new { r.FloorId, r.Placed })
            .Select(g => new { g.Key.FloorId, g.Key.Placed, Count = g.Count() }).ToListAsync(ct);

        return floors.Select(floor =>
        {
            var desks = deskGroups.Where(g => g.FloorId == floor.Id).ToList();
            var rooms = roomGroups.Where(g => g.FloorId == floor.Id).ToList();
            var placedDesks = desks.Where(g => g.Placed).Sum(g => g.Count);
            var unplacedDesks = desks.Where(g => !g.Placed).Sum(g => g.Count);
            var activeDesks = desks.Where(g => g.Active).Sum(g => g.Count);
            var closedDesks = desks.Where(g => !g.Active).Sum(g => g.Count);
            var placedRooms = rooms.Where(g => g.Placed).Sum(g => g.Count);
            var unplacedRooms = rooms.Where(g => !g.Placed).Sum(g => g.Count);
            return new FloorCapacityStats(floor.Id, floor.Number, floor.Name, floor.BuildingId, floor.Building.Name, floor.Building.Active, floor.Reservable,
                placedDesks + unplacedDesks, placedDesks, unplacedDesks, activeDesks, closedDesks,
                placedRooms + unplacedRooms, placedRooms, unplacedRooms);
        }).ToList();
    }

    private async Task ValidateDeskPlacementAsync(string floorId, string zoneId, int x, int y, CancellationToken ct)
    {
        var floor = await db.Floors.AsNoTracking().FirstOrDefaultAsync(f => f.Id == floorId, ct) ?? throw AppErrors.NotFound("Kat bulunamadı.");
        var zone = await db.Zones.AsNoTracking().FirstOrDefaultAsync(z => z.Id == zoneId, ct) ?? throw AppErrors.NotFound("Bölge bulunamadı.");
        if (zone.FloorId != floor.Id) throw AppErrors.Validation("Seçilen bölge bu kata ait değil.");
        if (x < 0 || y < 0 || x > floor.PlanWidth || y > floor.PlanHeight)
        {
            throw AppErrors.Validation("Masa koordinatları kat planı sınırlarının dışında.");
        }
    }

    public async Task<Desk> CreateDeskAsync(DeskInput input, AuditActor actor, CancellationToken ct = default)
    {
        await ValidateDeskPlacementAsync(input.FloorId, input.ZoneId, input.X, input.Y, ct);
        if (await db.Desks.AnyAsync(d => d.Code == input.Code, ct)) throw AppErrors.Conflict("Bu masa kodu zaten kullanılıyor.");

        var featureKeys = input.FeatureKeys ?? [];
        var features = await db.DeskFeatures.Where(f => featureKeys.Contains(f.Key)).ToListAsync(ct);

        var desk = new Desk
        {
            Code = input.Code, FloorId = input.FloorId, ZoneId = input.ZoneId, X = input.X, Y = input.Y,
            Active = input.Active, AccessibleOnly = input.AccessibleOnly, Popular = input.Popular, Notes = input.Notes,
        };
        foreach (var feature in features) desk.Features.Add(new DeskFeatureOnDesk { DeskId = desk.Id, FeatureId = feature.Id });
        db.Desks.Add(desk);
        await db.SaveChangesAsync(ct);
        desk.Floor = await db.Floors.FirstAsync(f => f.Id == desk.FloorId, ct);

        await audit.RecordAsync(actor, AuditActions.DeskCreate, "Desk", desk.Id,
            $"{desk.Code} masası {desk.Floor.Name} katına eklendi.", new { code = desk.Code }, ct);
        return desk;
    }

    public async Task<Desk> UpdateDeskAsync(string deskId, DeskUpdateInput input, AuditActor actor, CancellationToken ct = default)
    {
        var desk = await db.Desks.Include(d => d.Floor).Include(d => d.Zone).Include(d => d.Features).ThenInclude(l => l.Feature)
            .FirstOrDefaultAsync(d => d.Id == deskId, ct) ?? throw AppErrors.NotFound("Masa bulunamadı.");

        if (input.Code is not null && input.Code != desk.Code && await db.Desks.AnyAsync(d => d.Code == input.Code, ct))
        {
            throw AppErrors.Conflict("Bu masa kodu zaten kullanılıyor.");
        }

        if (input.FloorId is not null || input.ZoneId is not null || input.X is not null || input.Y is not null)
        {
            await ValidateDeskPlacementAsync(input.FloorId ?? desk.FloorId, input.ZoneId ?? desk.ZoneId, input.X ?? desk.X, input.Y ?? desk.Y, ct);
        }

        var closing = desk.Active && input.Active == false;
        var cancelled = await db.InTransactionAsync(async token =>
        {
            if (input.FeatureKeys is not null)
            {
                var keys = input.FeatureKeys;
                var features = await db.DeskFeatures.Where(f => keys.Contains(f.Key)).ToListAsync(token);
                desk.Features.Clear();
                foreach (var feature in features) desk.Features.Add(new DeskFeatureOnDesk { DeskId = desk.Id, FeatureId = feature.Id, Feature = feature });
            }
            if (input.Code is not null) desk.Code = input.Code;
            if (input.FloorId is not null) desk.FloorId = input.FloorId;
            if (input.ZoneId is not null) desk.ZoneId = input.ZoneId;
            if (input.X is int x) desk.X = x;
            if (input.Y is int y) desk.Y = y;
            if (input.Active is bool active) desk.Active = active;
            if (input.AccessibleOnly is bool ao) desk.AccessibleOnly = ao;
            if (input.Popular is bool popular) desk.Popular = popular;
            if (input.Notes.HasValue) desk.Notes = input.Notes.Value;
            await db.SaveChangesAsync(token);
            if (!closing) return 0;
            var notice = $"{desk.Code} masası kullanıma kapatıldı";
            return await canceller.CancelDesksAsync(r => r.DeskId == deskId, null, null, notice + ".", notice, actor, token);
        }, ct);

        await audit.RecordAsync(actor, AuditActions.DeskUpdate, "Desk", deskId,
            $"{desk.Code} masası güncellendi.{(input.Active == false ? " Masa kullanıma kapatıldı." : "")}{CancelledNote(cancelled)}",
            new { input.Code, input.FloorId, input.ZoneId, input.X, input.Y, input.Active, input.AccessibleOnly, input.Popular, Notes = input.Notes.Value }, ct);
        return desk;
    }

    public async Task DeleteDeskAsync(string deskId, AuditActor actor, CancellationToken ct = default)
    {
        var desk = await db.Desks.Select(d => new { d.Id, d.Code, ReservationCount = d.Reservations.Count }).FirstOrDefaultAsync(d => d.Id == deskId, ct)
            ?? throw AppErrors.NotFound("Masa bulunamadı.");
        if (desk.ReservationCount > 0)
        {
            throw AppErrors.Conflict("Bu masaya ait rezervasyon geçmişi bulunduğu için silinemez. Bunun yerine masayı kullanıma kapatabilirsiniz.");
        }

        await db.Desks.Where(d => d.Id == deskId).ExecuteDeleteAsync(ct);
        await audit.RecordAsync(actor, AuditActions.DeskDelete, "Desk", deskId, $"{desk.Code} masası silindi.", null, ct);
    }

    // --- Toplanti odalari ---

    public Task<List<RoomListItem>> ListRoomsAsync(string? floorId = null, string? search = null, CancellationToken ct = default)
    {
        var q = db.MeetingRooms.AsNoTracking().AsQueryable();
        if (floorId is not null) q = q.Where(r => r.FloorId == floorId);
        if (!string.IsNullOrEmpty(search))
        {
            var pattern = Search.Pattern(search);
            q = q.Where(r => EF.Functions.Like(r.Code.ToUpper().Replace("İ", "I"), pattern, Search.Escape) || EF.Functions.Like(r.Name.ToUpper().Replace("İ", "I"), pattern, Search.Escape));
        }
        return q.Include(r => r.Floor).ThenInclude(f => f.Building)
            .Include(r => r.Features).ThenInclude(l => l.Feature)
            .OrderBy(r => r.Floor.Building.Order).ThenBy(r => r.Floor.Number).ThenBy(r => r.Code)
            .Select(r => new RoomListItem(r, r.Reservations.Count))
            .ToListAsync(ct);
    }

    public async Task<MeetingRoom> CreateRoomAsync(RoomInput input, AuditActor actor, CancellationToken ct = default)
    {
        if (await db.MeetingRooms.AnyAsync(r => r.Code == input.Code, ct)) throw AppErrors.Conflict("Bu oda kodu zaten kullanılıyor.");
        if (input.Capacity < 1) throw AppErrors.Validation("Kapasite en az 1 olmalıdır.");
        // K2: kaynakta kat varligi dogrulanmiyordu (FK hatasi 500).
        var floor = await db.Floors.FirstOrDefaultAsync(f => f.Id == input.FloorId, ct) ?? throw AppErrors.NotFound("Kat bulunamadı.");

        var featureKeys = input.FeatureKeys ?? [];
        var features = await db.RoomFeatures.Where(f => featureKeys.Contains(f.Key)).ToListAsync(ct);
        var room = new MeetingRoom
        {
            Code = input.Code, Name = input.Name, FloorId = input.FloorId, Capacity = input.Capacity,
            X = input.X, Y = input.Y, Width = input.Width, Height = input.Height, Active = input.Active, Notes = input.Notes,
        };
        foreach (var feature in features) room.Features.Add(new RoomFeatureOnRoom { RoomId = room.Id, FeatureId = feature.Id });
        db.MeetingRooms.Add(room);
        await db.SaveChangesAsync(ct);
        room.Floor = floor;

        await audit.RecordAsync(actor, AuditActions.RoomCreate, "MeetingRoom", room.Id,
            $"{room.Name} ({room.Code}) toplantı odası eklendi.", new { code = room.Code, capacity = room.Capacity }, ct);
        return room;
    }

    public async Task<MeetingRoom> UpdateRoomAsync(string roomId, RoomUpdateInput input, AuditActor actor, CancellationToken ct = default)
    {
        var room = await db.MeetingRooms.Include(r => r.Floor).Include(r => r.Features).ThenInclude(l => l.Feature)
            .FirstOrDefaultAsync(r => r.Id == roomId, ct) ?? throw AppErrors.NotFound("Toplantı odası bulunamadı.");

        if (input.Code is not null && input.Code != room.Code && await db.MeetingRooms.AnyAsync(r => r.Code == input.Code, ct))
        {
            throw AppErrors.Conflict("Bu oda kodu zaten kullanılıyor.");
        }
        if (input.Capacity is int capacity && capacity < 1) throw AppErrors.Validation("Kapasite en az 1 olmalıdır.");

        var closing = room.Active && input.Active == false;
        var cancelled = await db.InTransactionAsync(async token =>
        {
            if (input.FeatureKeys is not null)
            {
                var keys = input.FeatureKeys;
                var features = await db.RoomFeatures.Where(f => keys.Contains(f.Key)).ToListAsync(token);
                room.Features.Clear();
                foreach (var feature in features) room.Features.Add(new RoomFeatureOnRoom { RoomId = room.Id, FeatureId = feature.Id, Feature = feature });
            }
            if (input.Code is not null) room.Code = input.Code;
            if (input.Name is not null) room.Name = input.Name;
            if (input.FloorId is not null) room.FloorId = input.FloorId;
            if (input.Capacity is int c) room.Capacity = c;
            if (input.X is int x) room.X = x;
            if (input.Y is int y) room.Y = y;
            if (input.Width is int w) room.Width = w;
            if (input.Height is int h) room.Height = h;
            if (input.Active is bool active) room.Active = active;
            if (input.Notes.HasValue) room.Notes = input.Notes.Value;
            await db.SaveChangesAsync(token);
            if (!closing) return 0;
            var notice = $"{room.Name} odası kullanıma kapatıldı";
            return await canceller.CancelRoomsAsync(r => r.RoomId == roomId, null, null, notice + ".", notice, actor, token);
        }, ct);

        await audit.RecordAsync(actor, AuditActions.RoomUpdate, "MeetingRoom", roomId,
            $"{room.Name} ({room.Code}) güncellendi.{(input.Active == false ? " Oda kullanıma kapatıldı." : "")}{CancelledNote(cancelled)}",
            new { input.Code, input.Name, input.FloorId, input.Capacity, input.X, input.Y, input.Width, input.Height, input.Active, Notes = input.Notes.Value }, ct);
        return room;
    }

    public async Task DeleteRoomAsync(string roomId, AuditActor actor, CancellationToken ct = default)
    {
        var room = await db.MeetingRooms.Select(r => new { r.Id, r.Code, r.Name, ReservationCount = r.Reservations.Count }).FirstOrDefaultAsync(r => r.Id == roomId, ct)
            ?? throw AppErrors.NotFound("Toplantı odası bulunamadı.");
        if (room.ReservationCount > 0)
        {
            throw AppErrors.Conflict("Bu odaya ait rezervasyon geçmişi bulunduğu için silinemez. Bunun yerine odayı kullanıma kapatabilirsiniz.");
        }

        await db.MeetingRooms.Where(r => r.Id == roomId).ExecuteDeleteAsync(ct);
        await audit.RecordAsync(actor, AuditActions.RoomDelete, "MeetingRoom", roomId, $"{room.Name} ({room.Code}) silindi.", null, ct);
    }

    public Task<List<DeskFeature>> ListDeskFeaturesAsync(CancellationToken ct = default) =>
        db.DeskFeatures.AsNoTracking().OrderBy(f => f.Name).ToListAsync(ct);

    public Task<List<RoomFeature>> ListRoomFeaturesAsync(CancellationToken ct = default) =>
        db.RoomFeatures.AsNoTracking().OrderBy(f => f.Name).ToListAsync(ct);
}
