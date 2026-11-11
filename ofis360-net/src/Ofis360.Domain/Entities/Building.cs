using Ofis360.Domain.Common;

namespace Ofis360.Domain.Entities;

/// <summary>Bina: katlar bir binaya baglidir. Pasif bina calisan ekranlarinda ve raporlarda gorunmez.</summary>
public class Building
{
    public string Id { get; set; } = Ids.New();
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    /// <summary>Kat sekmeleri ve raporlarda gosterim sirasi.</summary>
    public int Order { get; set; }
    public bool Active { get; set; } = true;
    /// <summary>Demo/kurgusal bina: canliya cikista temizlenecek veriyi isaretler.</summary>
    public bool IsDemo { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<Floor> Floors { get; set; } = new List<Floor>();
}

public class Floor
{
    public string Id { get; set; } = Ids.New();
    public string BuildingId { get; set; } = string.Empty;
    public int Number { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Reservable { get; set; } = true;
    public bool Active { get; set; } = true;
    public int PlanWidth { get; set; } = 1000;
    public int PlanHeight { get; set; } = 640;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Building Building { get; set; } = null!;
    public ICollection<Zone> Zones { get; set; } = new List<Zone>();
    public ICollection<FloorPlanElement> Elements { get; set; } = new List<FloorPlanElement>();
    public ICollection<Desk> Desks { get; set; } = new List<Desk>();
    public ICollection<MeetingRoom> Rooms { get; set; } = new List<MeetingRoom>();
    public ICollection<Closure> Closures { get; set; } = new List<Closure>();
}

public class Zone
{
    public string Id { get; set; } = Ids.New();
    public string FloorId { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = ZoneKinds.Work;
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public bool WindowSide { get; set; }
    public bool Quiet { get; set; }
    public bool Reservable { get; set; } = true;

    public Floor Floor { get; set; } = null!;
    public ICollection<Desk> Desks { get; set; } = new List<Desk>();
}
