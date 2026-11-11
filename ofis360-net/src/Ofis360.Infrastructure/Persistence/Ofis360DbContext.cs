using System.Data;
using Microsoft.EntityFrameworkCore;
using Ofis360.Application.Persistence;
using Ofis360.Domain.Entities;
using Oracle.ManagedDataAccess.Client;

namespace Ofis360.Infrastructure.Persistence;

public class Ofis360DbContext(DbContextOptions<Ofis360DbContext> options) : DbContext(options), IOfis360Db
{
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Building> Buildings => Set<Building>();
    public DbSet<Floor> Floors => Set<Floor>();
    public DbSet<Zone> Zones => Set<Zone>();
    public DbSet<DeskFeature> DeskFeatures => Set<DeskFeature>();
    public DbSet<Desk> Desks => Set<Desk>();
    public DbSet<DeskFeatureOnDesk> DeskFeatureLinks => Set<DeskFeatureOnDesk>();
    public DbSet<RoomFeature> RoomFeatures => Set<RoomFeature>();
    public DbSet<MeetingRoom> MeetingRooms => Set<MeetingRoom>();
    public DbSet<RoomFeatureOnRoom> RoomFeatureLinks => Set<RoomFeatureOnRoom>();
    public DbSet<DeskReservation> DeskReservations => Set<DeskReservation>();
    public DbSet<RoomReservation> RoomReservations => Set<RoomReservation>();
    public DbSet<ReservationRule> ReservationRules => Set<ReservationRule>();
    public DbSet<ReservationRuleOverride> ReservationRuleOverrides => Set<ReservationRuleOverride>();
    public DbSet<Holiday> Holidays => Set<Holiday>();
    public DbSet<AppSession> AppSessions => Set<AppSession>();
    public DbSet<RoomAccessRule> RoomAccessRules => Set<RoomAccessRule>();
    public DbSet<RoomPriorityUser> RoomPriorityUsers => Set<RoomPriorityUser>();
    public DbSet<CapacityShortfall> CapacityShortfalls => Set<CapacityShortfall>();
    public DbSet<Closure> Closures => Set<Closure>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<FloorPlanElement> FloorPlanElements => Set<FloorPlanElement>();

    public IQueryable<FloorDayOccupancyRow> FloorDayOccupancy => Set<FloorDayOccupancyRow>().AsNoTracking();
    public IQueryable<CheckInHourlyRow> CheckInHourly => Set<CheckInHourlyRow>().AsNoTracking();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(Ofis360DbContext).Assembly);

        // Oracle view'lari: migration'lar tarafindan yonetilmez (Sql/OracleObjects.cs ile olusturulur).
        modelBuilder.Entity<FloorDayOccupancyRow>(b =>
        {
            b.HasNoKey().ToView("V_FLOOR_DAY_OCCUPANCY");
            b.Property(x => x.FloorId).HasColumnName("FLOOR_ID");
            b.Property(x => x.Day).HasColumnName("DAY");
            b.Property(x => x.Count).HasColumnName("N");
        });
        modelBuilder.Entity<CheckInHourlyRow>(b =>
        {
            b.HasNoKey().ToView("V_CHECKIN_HOURLY");
            b.Property(x => x.Day).HasColumnName("DAY");
            b.Property(x => x.Hour).HasColumnName("HOUR");
            b.Property(x => x.Count).HasColumnName("N");
        });
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<string>().HaveMaxLength(400);
        configurationBuilder.Properties<DateTimeOffset>().HaveColumnType("TIMESTAMP(3) WITH TIME ZONE");
        configurationBuilder.Properties<DateOnly>()
            .HaveConversion<DateOnlyConverter, DateOnlyComparer>()
            .HaveColumnType("DATE");
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        TouchUpdatedAt();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        TouchUpdatedAt();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// Prisma @updatedAt karsiligi: Prisma bu alani istemci tarafinda yazar, DB trigger'i degil.
    /// Hedefte de ayni sekilde SaveChanges sirasinda degistirilen kayitlarda UPDATED_AT = simdi.
    /// </summary>
    private void TouchUpdatedAt()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State != EntityState.Modified) continue;
            var property = entry.Metadata.FindProperty("UpdatedAt");
            if (property is null || property.ClrType != typeof(DateTimeOffset)) continue;
            entry.Property("UpdatedAt").CurrentValue = now;
        }
    }

    public async Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken = default)
    {
        if (Database.CurrentTransaction is not null)
        {
            return await work(cancellationToken);
        }

        await using var transaction = await Database.BeginTransactionAsync(cancellationToken);
        var result = await work(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task LockUserAsync(string userId, CancellationToken cancellationToken = default) =>
        await Database.ExecuteSqlInterpolatedAsync($"SELECT ID FROM APP_USER WHERE ID = {userId} FOR UPDATE WAIT 10", cancellationToken);

    public async Task LockActiveAdminsAsync(CancellationToken cancellationToken = default) =>
        await Database.ExecuteSqlInterpolatedAsync($"SELECT ID FROM APP_USER WHERE ROLE = {Ofis360.Domain.Common.Roles.Admin} AND ACTIVE = 1 ORDER BY ID FOR UPDATE WAIT 10", cancellationToken);

    public async Task<bool> BookRoomAsync(RoomReservation r, CancellationToken cancellationToken = default)
    {
        var parameters = new object[]
        {
            new OracleParameter("p_id", OracleDbType.Varchar2, r.Id, ParameterDirection.Input),
            new OracleParameter("p_room_id", OracleDbType.Varchar2, r.RoomId, ParameterDirection.Input),
            new OracleParameter("p_user_id", OracleDbType.Varchar2, r.UserId, ParameterDirection.Input),
            new OracleParameter("p_day", OracleDbType.Date, r.Date.ToDateTime(TimeOnly.MinValue), ParameterDirection.Input),
            new OracleParameter("p_start_at", OracleDbType.TimeStampTZ, r.StartAt, ParameterDirection.Input),
            new OracleParameter("p_end_at", OracleDbType.TimeStampTZ, r.EndAt, ParameterDirection.Input),
            new OracleParameter("p_start_min", OracleDbType.Int32, r.StartMinutes, ParameterDirection.Input),
            new OracleParameter("p_end_min", OracleDbType.Int32, r.EndMinutes, ParameterDirection.Input),
            new OracleParameter("p_title", OracleDbType.NVarchar2, r.Title, ParameterDirection.Input),
            new OracleParameter("p_purpose", OracleDbType.NVarchar2, r.Purpose, ParameterDirection.Input),
            new OracleParameter("p_attendees", OracleDbType.Int32, r.AttendeeCount, ParameterDirection.Input),
            new OracleParameter("p_source", OracleDbType.Varchar2, r.Source, ParameterDirection.Input),
            new OracleParameter("p_now", OracleDbType.TimeStampTZ, r.CreatedAt, ParameterDirection.Input),
        };

        try
        {
            await Database.ExecuteSqlRawAsync(
                "BEGIN OFIS360_RESERVATION_PKG.BOOK_ROOM(:p_id, :p_room_id, :p_user_id, :p_day, :p_start_at, :p_end_at, " +
                ":p_start_min, :p_end_min, :p_title, :p_purpose, :p_attendees, :p_source, :p_now); END;",
                parameters,
                cancellationToken);
            return true;
        }
        catch (OracleException ex) when (ex.Number == 20001)
        {
            return false;
        }
    }

    public bool IsUniqueViolation(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is OracleException { Number: 1 }) return true;
        }
        return false;
    }
}
