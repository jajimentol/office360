using Microsoft.EntityFrameworkCore;
using Ofis360.Domain.Entities;

namespace Ofis360.Application.Persistence;

/// <summary>
/// Servislerin veritabani erisim seam'i. Infrastructure'daki Ofis360DbContext bunu uygular; servisler
/// LINQ ile dogrudan DbSet'ler uzerinde calisir (repository katmani yok, haritadaki karar).
/// </summary>
public interface IOfis360Db
{
    DbSet<Department> Departments { get; }
    DbSet<AppUser> Users { get; }
    DbSet<Building> Buildings { get; }
    DbSet<Floor> Floors { get; }
    DbSet<Zone> Zones { get; }
    DbSet<DeskFeature> DeskFeatures { get; }
    DbSet<Desk> Desks { get; }
    DbSet<DeskFeatureOnDesk> DeskFeatureLinks { get; }
    DbSet<RoomFeature> RoomFeatures { get; }
    DbSet<MeetingRoom> MeetingRooms { get; }
    DbSet<RoomFeatureOnRoom> RoomFeatureLinks { get; }
    DbSet<DeskReservation> DeskReservations { get; }
    DbSet<RoomReservation> RoomReservations { get; }
    DbSet<ReservationRule> ReservationRules { get; }
    DbSet<ReservationRuleOverride> ReservationRuleOverrides { get; }
    DbSet<Holiday> Holidays { get; }
    DbSet<AppSession> AppSessions { get; }
    DbSet<RoomAccessRule> RoomAccessRules { get; }
    DbSet<RoomPriorityUser> RoomPriorityUsers { get; }
    DbSet<CapacityShortfall> CapacityShortfalls { get; }
    DbSet<Closure> Closures { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<SystemSetting> SystemSettings { get; }
    DbSet<FloorPlanElement> FloorPlanElements { get; }

    /// <summary>Oracle view V_FLOOR_DAY_OCCUPANCY (kat x gun aktif masa rezervasyonu, aktif binalar).</summary>
    IQueryable<FloorDayOccupancyRow> FloorDayOccupancy { get; }

    /// <summary>Oracle view V_CHECKIN_HOURLY (Istanbul saati x gun giris sayisi, aktif binalar).</summary>
    IQueryable<CheckInHourlyRow> CheckInHourly { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Isi tek transaction icinde calistirir (Prisma $transaction karsiligi).</summary>
    Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken = default);

    /// <summary>
    /// OFIS360_RESERVATION_PKG.BOOK_ROOM: oda satirini kilitler, cakisma yoksa INSERT eder.
    /// Cakismada false doner (ORA-20001). Kaynakta DB duzeyinde korunmayan es zamanli oda cakismasi (K5).
    /// </summary>
    Task<bool> BookRoomAsync(RoomReservation reservation, CancellationToken cancellationToken = default);

    /// <summary>Kullanici satirini transaction sonuna kadar kilitler; ayni kisinin es zamanli rezervasyonlari sirayla degerlendirilir.</summary>
    Task LockUserAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>Aktif admin satirlarini transaction sonuna kadar kilitler; son admin korumasi es zamanli islemlerde de gecerli kalir.</summary>
    Task LockActiveAdminsAsync(CancellationToken cancellationToken = default);

    /// <summary>Son hata ORA-00001 (unique) mi? K4: yalnizca bu durum CONFLICT'e cevrilir.</summary>
    bool IsUniqueViolation(Exception exception);
}

/// <summary>V_FLOOR_DAY_OCCUPANCY satiri (keyless).</summary>
public sealed class FloorDayOccupancyRow
{
    public string FloorId { get; set; } = string.Empty;
    public DateOnly Day { get; set; }
    public int Count { get; set; }
}

/// <summary>V_CHECKIN_HOURLY satiri (keyless).</summary>
public sealed class CheckInHourlyRow
{
    public DateOnly Day { get; set; }
    public int Hour { get; set; }
    public int Count { get; set; }
}
