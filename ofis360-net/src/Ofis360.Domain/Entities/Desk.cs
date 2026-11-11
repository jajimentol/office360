using Ofis360.Domain.Common;

namespace Ofis360.Domain.Entities;

public class DeskFeature
{
    public string Id { get; set; } = Ids.New();
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;

    public ICollection<DeskFeatureOnDesk> Desks { get; set; } = new List<DeskFeatureOnDesk>();
}

public class Desk
{
    public string Id { get; set; } = Ids.New();
    public string Code { get; set; } = string.Empty;
    public string FloorId { get; set; } = string.Empty;
    public string ZoneId { get; set; } = string.Empty;
    public int X { get; set; }
    public int Y { get; set; }
    /// <summary>Krokideki gorsel olculer; varsayilanlar mevcut masalarin boyutunu korur.</summary>
    public int Width { get; set; } = FloorPlanConfig.DeskWidth;
    public int Height { get; set; } = FloorPlanConfig.DeskHeight;
    public int Rotation { get; set; }
    /// <summary>Masa krokiye yerlestirildi mi? Yerlestirilmemis masa calisan rezervasyonuna kapalidir.</summary>
    public bool Placed { get; set; } = true;
    public bool Active { get; set; } = true;
    public bool AccessibleOnly { get; set; }
    public bool Popular { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Floor Floor { get; set; } = null!;
    public Zone Zone { get; set; } = null!;
    public ICollection<DeskFeatureOnDesk> Features { get; set; } = new List<DeskFeatureOnDesk>();
    public ICollection<DeskReservation> Reservations { get; set; } = new List<DeskReservation>();
    public ICollection<Closure> Closures { get; set; } = new List<Closure>();
}

public class DeskFeatureOnDesk
{
    public string DeskId { get; set; } = string.Empty;
    public string FeatureId { get; set; } = string.Empty;

    public Desk Desk { get; set; } = null!;
    public DeskFeature Feature { get; set; } = null!;
}

public class RoomFeature
{
    public string Id { get; set; } = Ids.New();
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;

    public ICollection<RoomFeatureOnRoom> Rooms { get; set; } = new List<RoomFeatureOnRoom>();
}

public class MeetingRoom
{
    public string Id { get; set; } = Ids.New();
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string FloorId { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public bool Placed { get; set; } = true;
    public bool Active { get; set; } = true;
    public string? Notes { get; set; }
    /// <summary>Yalnizca Sekreter rezerve edebilir.</summary>
    public bool SecretaryOnly { get; set; }
    /// <summary>Oncelikli kisi varsa digerlerinin odayi alabildigi son is gunu sayisi.</summary>
    public int? PriorityOpenDays { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Floor Floor { get; set; } = null!;
    public ICollection<RoomFeatureOnRoom> Features { get; set; } = new List<RoomFeatureOnRoom>();
    public ICollection<RoomReservation> Reservations { get; set; } = new List<RoomReservation>();
    public ICollection<Closure> Closures { get; set; } = new List<Closure>();
    public ICollection<RoomAccessRule> AccessRules { get; set; } = new List<RoomAccessRule>();
    public ICollection<RoomPriorityUser> PriorityUsers { get; set; } = new List<RoomPriorityUser>();
}

/// <summary>Bu kademe odayi en az bu kadar katilimciyla rezerve edebilir.</summary>
public class RoomAccessRule
{
    public string Id { get; set; } = Ids.New();
    public string RoomId { get; set; } = string.Empty;
    public string Grade { get; set; } = string.Empty;
    public int MinAttendees { get; set; } = 1;
    /// <summary>Oncelikli kisilerin penceresini beklemez.</summary>
    public bool BypassPriority { get; set; }

    public MeetingRoom Room { get; set; } = null!;
}

/// <summary>Odada oncelikli kisi (or. bolge muduru).</summary>
public class RoomPriorityUser
{
    public string RoomId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;

    public MeetingRoom Room { get; set; } = null!;
    public AppUser User { get; set; } = null!;
}

public class RoomFeatureOnRoom
{
    public string RoomId { get; set; } = string.Empty;
    public string FeatureId { get; set; } = string.Empty;

    public MeetingRoom Room { get; set; } = null!;
    public RoomFeature Feature { get; set; } = null!;
}
