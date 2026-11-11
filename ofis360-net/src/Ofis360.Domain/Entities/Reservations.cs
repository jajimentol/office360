using Ofis360.Domain.Common;

namespace Ofis360.Domain.Entities;

/// <summary>
/// ActiveDeskKey / ActiveUserDayKey: yalnizca aktif (iptal edilmemis) kayitlarda dolu, iptalde NULL'a cekilen
/// alanlar. Oracle tek sutunlu unique index'te NULL'lari indekslemediginden "cift rezervasyon" ve
/// "ayni gune ikinci masa" veritabani seviyesinde de engellenir (SQLite ile ayni semantik).
/// </summary>
public class DeskReservation
{
    public string Id { get; set; } = Ids.New();
    public string DeskId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    /// <summary>Takvim gunu (kaynakta UTC gece yarisi DateTime; hedefte DateOnly ↔ Oracle DATE).</summary>
    public DateOnly Date { get; set; }
    public string Status { get; set; } = DeskReservationStatus.Booked;
    public string? ActiveDeskKey { get; set; }
    public string? ActiveUserDayKey { get; set; }
    public string? Note { get; set; }
    public string Source { get; set; } = ReservationSources.Employee;
    public DateTimeOffset? CheckedInAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public string? CancelledById { get; set; }
    public string? CancelReason { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Desk Desk { get; set; } = null!;
    public AppUser User { get; set; } = null!;

    public string DayKey => DayKeys.FromDateOnly(Date);

    public static string BuildActiveDeskKey(string deskId, string dayKey) => $"{deskId}#{dayKey}";
    public static string BuildActiveUserDayKey(string userId, string dayKey) => $"{userId}#{dayKey}";
}

public class RoomReservation
{
    public string Id { get; set; } = Ids.New();
    public string RoomId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    /// <summary>Gun anahtari + dakika (UTC tabanli, kaynak: dayKeyWithMinutes).</summary>
    public DateTimeOffset StartAt { get; set; }
    public DateTimeOffset EndAt { get; set; }
    public int StartMinutes { get; set; }
    public int EndMinutes { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public int AttendeeCount { get; set; }
    public string Status { get; set; } = RoomReservationStatus.Booked;
    /// <summary>"Toplanti basladi" isaretlendigi an; bos toplanti raporu buna bakar.</summary>
    public DateTimeOffset? StartedAt { get; set; }
    public string Source { get; set; } = ReservationSources.Employee;
    public DateTimeOffset? CancelledAt { get; set; }
    public string? CancelledById { get; set; }
    public string? CancelReason { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public MeetingRoom Room { get; set; } = null!;
    public AppUser User { get; set; } = null!;

    public string DayKey => DayKeys.FromDateOnly(Date);
}

public class ReservationRule
{
    public string Id { get; set; } = Ids.New();
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = RuleCategories.Desk;
    public bool Enabled { get; set; } = true;
    public bool EditableLimit { get; set; } = true;
    public int? IntValue { get; set; }
    public int? SecondValue { get; set; }
    public string? Unit { get; set; }
    public int? MinValue { get; set; }
    public int? MaxValue { get; set; }
    public bool SystemRule { get; set; }
    public int Order { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedById { get; set; }
}

/// <summary>Kurala kademe (istege bagli bina) bazli deger. Null alan ust seviyeden gelir.</summary>
public class ReservationRuleOverride
{
    public string Id { get; set; } = Ids.New();
    public string RuleKey { get; set; } = string.Empty;
    public string Grade { get; set; } = string.Empty;
    public string? BuildingId { get; set; }
    public bool? Enabled { get; set; }
    public int? IntValue { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedById { get; set; }

    public Building? Building { get; set; }
}

/// <summary>Resmi tatil: is gunu hesabindan duser. Dini bayramlar her yil degistigi icin admin girer.</summary>
public class Holiday
{
    public string Id { get; set; } = Ids.New();
    public DateOnly Date { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? CreatedById { get; set; }

    public string DayKey => DayKeys.FromDateOnly(Date);
}

/// <summary>
/// Gunluk kapasite acigi - toplulastirilmis kayit. Kisisel veri icermez: gun bazinda
/// talep/karsilanan/karsilanamayan sayilari tutulur.
/// </summary>
public class CapacityShortfall
{
    public string Id { get; set; } = Ids.New();
    public DateOnly Date { get; set; }
    /// <summary>O gun masa talebinde bulunan calisan sayisi.</summary>
    public int Requests { get; set; }
    /// <summary>Masa tahsis edilen calisan sayisi.</summary>
    public int Fulfilled { get; set; }
    /// <summary>Kapasite dolu oldugu icin masa bulamayan calisan sayisi.</summary>
    public int Unmet { get; set; }
    public string Source { get; set; } = ShortfallSources.System;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class Closure
{
    public string Id { get; set; } = Ids.New();
    public string Scope { get; set; } = ClosureScopes.Floor;
    public string? FloorId { get; set; }
    public string? DeskId { get; set; }
    public string? RoomId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string Reason { get; set; } = ClosureReasons.Other;
    public string Note { get; set; } = string.Empty;
    public bool Active { get; set; } = true;
    public string CreatedById { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Floor? Floor { get; set; }
    public Desk? Desk { get; set; }
    public MeetingRoom? Room { get; set; }
}

public class Notification
{
    public string Id { get; set; } = Ids.New();
    public string UserId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? Link { get; set; }
    public string Severity { get; set; } = NotificationSeverities.Info;
    public DateTimeOffset? ReadAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public AppUser User { get; set; } = null!;
}

public class AuditLog
{
    public string Id { get; set; } = Ids.New();
    public string? ActorId { get; set; }
    public string ActorName { get; set; } = string.Empty;
    public string ActorRole { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string Summary { get; set; } = string.Empty;
    /// <summary>JSON (Oracle CLOB, CHECK IS JSON).</summary>
    public string? Metadata { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Adminin uygulama icinde cizdigi kat krokisi ogesi. Rezervasyon kayitlarindan bagimsizdir:
/// kroki silinse bile masa, oda ve rezervasyonlar korunur.
/// </summary>
public class FloorPlanElement
{
    public string Id { get; set; } = Ids.New();
    public string FloorId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Label { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public int Rotation { get; set; }
    /// <summary>JSON: { fill, stroke, strokeWidth, fontSize }</summary>
    public string Style { get; set; } = "{}";
    public int LayerOrder { get; set; }
    public bool Locked { get; set; }
    public bool Visible { get; set; } = true;
    public string? LinkedResourceId { get; set; }
    public string? LinkedResourceType { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Floor Floor { get; set; } = null!;
}
