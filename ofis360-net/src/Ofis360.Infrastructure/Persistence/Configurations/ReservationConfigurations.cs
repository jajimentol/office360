using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ofis360.Domain.Common;
using Ofis360.Domain.Entities;

namespace Ofis360.Infrastructure.Persistence.Configurations;

// DATE Oracle'da ayrilmis sozcuk → RESERVATION_DATE / SHORTFALL_DATE. Takvim gunu DateOnly ↔ DATE.

public sealed class DeskReservationConfiguration : IEntityTypeConfiguration<DeskReservation>
{
    public void Configure(EntityTypeBuilder<DeskReservation> b)
    {
        b.ToTable("DESK_RESERVATION");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("ID").HasMaxLength(Ids.Length).ValueGeneratedNever();
        b.Property(x => x.DeskId).HasColumnName("DESK_ID").HasMaxLength(Ids.Length).IsRequired();
        b.Property(x => x.UserId).HasColumnName("USER_ID").HasMaxLength(Ids.Length).IsRequired();
        b.Property(x => x.Date).HasColumnName("RESERVATION_DATE").IsRequired();
        b.Property(x => x.Status).HasColumnName("STATUS").HasMaxLength(20).IsRequired().HasDefaultValue(DeskReservationStatus.Booked);
        // deskId:dayKey ve userId:dayKey → 32 + 1 + 10
        b.Property(x => x.ActiveDeskKey).HasColumnName("ACTIVE_DESK_KEY").HasMaxLength(48);
        b.Property(x => x.ActiveUserDayKey).HasColumnName("ACTIVE_USER_DAY_KEY").HasMaxLength(48);
        b.Property(x => x.Note).HasColumnName("NOTE").HasMaxLength(240);
        b.Property(x => x.Source).HasColumnName("SOURCE").HasMaxLength(20).IsRequired().HasDefaultValue(ReservationSources.Employee);
        b.Property(x => x.CheckedInAt).HasColumnName("CHECKED_IN_AT");
        b.Property(x => x.CancelledAt).HasColumnName("CANCELLED_AT");
        b.Property(x => x.CancelledById).HasColumnName("CANCELLED_BY_ID").HasMaxLength(Ids.Length);
        b.Property(x => x.CancelReason).HasColumnName("CANCEL_REASON").HasMaxLength(240);
        b.Property(x => x.CreatedAt).HasColumnName("CREATED_AT").IsRequired();
        b.Property(x => x.UpdatedAt).HasColumnName("UPDATED_AT").IsRequired();
        b.Ignore(x => x.DayKey);

        b.HasOne(x => x.Desk).WithMany(x => x.Reservations).HasForeignKey(x => x.DeskId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.User).WithMany(x => x.DeskReservations).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);

        // Oracle tek sutunlu unique index'te NULL'lari indekslemez → iptal edilmis kayitlar cakismaz.
        b.HasIndex(x => x.ActiveDeskKey).IsUnique().HasDatabaseName("UX_DESK_RES_ACTIVE_DESK");
        b.HasIndex(x => x.ActiveUserDayKey).IsUnique().HasDatabaseName("UX_DESK_RES_ACTIVE_USER_DAY");
        b.HasIndex(x => x.Date).HasDatabaseName("IX_DESK_RES_DATE");
        b.HasIndex(x => new { x.UserId, x.Date }).HasDatabaseName("IX_DESK_RES_USER_DATE");
        b.HasIndex(x => new { x.DeskId, x.Date }).HasDatabaseName("IX_DESK_RES_DESK_DATE");
        b.HasIndex(x => x.Status).HasDatabaseName("IX_DESK_RES_STATUS");
        b.HasIndex(x => new { x.Date, x.Status }).HasDatabaseName("IX_DESK_RES_DATE_STATUS");
        b.HasIndex(x => new { x.DeskId, x.Date, x.Status }).HasDatabaseName("IX_DESK_RES_DESK_DATE_STATUS");
        b.HasIndex(x => new { x.UserId, x.Date, x.Status }).HasDatabaseName("IX_DESK_RES_USER_DATE_STATUS");
    }
}

public sealed class RoomReservationConfiguration : IEntityTypeConfiguration<RoomReservation>
{
    public void Configure(EntityTypeBuilder<RoomReservation> b)
    {
        b.ToTable("ROOM_RESERVATION");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("ID").HasMaxLength(Ids.Length).ValueGeneratedNever();
        b.Property(x => x.RoomId).HasColumnName("ROOM_ID").HasMaxLength(Ids.Length).IsRequired();
        b.Property(x => x.UserId).HasColumnName("USER_ID").HasMaxLength(Ids.Length).IsRequired();
        b.Property(x => x.Date).HasColumnName("RESERVATION_DATE").IsRequired();
        b.Property(x => x.StartAt).HasColumnName("START_AT").IsRequired();
        b.Property(x => x.EndAt).HasColumnName("END_AT").IsRequired();
        b.Property(x => x.StartMinutes).HasColumnName("START_MINUTES").IsRequired();
        b.Property(x => x.StartedAt).HasColumnName("STARTED_AT");
        b.Property(x => x.EndMinutes).HasColumnName("END_MINUTES").IsRequired();
        b.Property(x => x.Title).HasColumnName("TITLE").HasMaxLength(120).IsRequired();
        b.Property(x => x.Purpose).HasColumnName("PURPOSE").HasMaxLength(120).IsRequired();
        b.Property(x => x.AttendeeCount).HasColumnName("ATTENDEE_COUNT").IsRequired();
        b.Property(x => x.Status).HasColumnName("STATUS").HasMaxLength(20).IsRequired().HasDefaultValue(RoomReservationStatus.Booked);
        b.Property(x => x.Source).HasColumnName("SOURCE").HasMaxLength(20).IsRequired().HasDefaultValue(ReservationSources.Employee);
        b.Property(x => x.CancelledAt).HasColumnName("CANCELLED_AT");
        b.Property(x => x.CancelledById).HasColumnName("CANCELLED_BY_ID").HasMaxLength(Ids.Length);
        b.Property(x => x.CancelReason).HasColumnName("CANCEL_REASON").HasMaxLength(240);
        b.Property(x => x.CreatedAt).HasColumnName("CREATED_AT").IsRequired();
        b.Property(x => x.UpdatedAt).HasColumnName("UPDATED_AT").IsRequired();
        b.Ignore(x => x.DayKey);

        b.HasOne(x => x.Room).WithMany(x => x.Reservations).HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.User).WithMany(x => x.RoomReservations).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.Date).HasDatabaseName("IX_ROOM_RES_DATE");
        b.HasIndex(x => new { x.RoomId, x.Date }).HasDatabaseName("IX_ROOM_RES_ROOM_DATE");
        b.HasIndex(x => new { x.UserId, x.Date }).HasDatabaseName("IX_ROOM_RES_USER_DATE");
        b.HasIndex(x => x.Status).HasDatabaseName("IX_ROOM_RES_STATUS");
        b.HasIndex(x => new { x.Date, x.Status }).HasDatabaseName("IX_ROOM_RES_DATE_STATUS");
        b.HasIndex(x => new { x.RoomId, x.Date, x.Status }).HasDatabaseName("IX_ROOM_RES_ROOM_DATE_STATUS");
    }
}

public sealed class ReservationRuleConfiguration : IEntityTypeConfiguration<ReservationRule>
{
    public void Configure(EntityTypeBuilder<ReservationRule> b)
    {
        b.ToTable("RESERVATION_RULE");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("ID").HasMaxLength(Ids.Length).ValueGeneratedNever();
        b.Property(x => x.Key).HasColumnName("RULE_KEY").HasMaxLength(50).IsRequired();
        b.Property(x => x.Name).HasColumnName("NAME").HasMaxLength(120).IsRequired();
        b.Property(x => x.Description).HasColumnName("DESCRIPTION").HasMaxLength(400).IsRequired();
        b.Property(x => x.Category).HasColumnName("CATEGORY").HasMaxLength(20).IsRequired().HasDefaultValue(RuleCategories.Desk);
        b.Property(x => x.Enabled).HasColumnName("ENABLED").IsRequired().HasDefaultValue(true);
        b.Property(x => x.EditableLimit).HasColumnName("EDITABLE_LIMIT").IsRequired().HasDefaultValue(true);
        b.Property(x => x.IntValue).HasColumnName("INT_VALUE");
        b.Property(x => x.SecondValue).HasColumnName("SECOND_VALUE");
        b.Property(x => x.Unit).HasColumnName("UNIT").HasMaxLength(60);
        b.Property(x => x.MinValue).HasColumnName("MIN_VALUE");
        b.Property(x => x.MaxValue).HasColumnName("MAX_VALUE");
        b.Property(x => x.SystemRule).HasColumnName("SYSTEM_RULE").IsRequired().HasDefaultValue(false);
        b.Property(x => x.Order).HasColumnName("SORT_ORDER").IsRequired().HasDefaultValue(0);
        b.Property(x => x.UpdatedAt).HasColumnName("UPDATED_AT").IsRequired();
        b.Property(x => x.UpdatedById).HasColumnName("UPDATED_BY_ID").HasMaxLength(Ids.Length);
        b.HasIndex(x => x.Key).IsUnique().HasDatabaseName("UX_RESERVATION_RULE_KEY");
    }
}

public sealed class ReservationRuleOverrideConfiguration : IEntityTypeConfiguration<ReservationRuleOverride>
{
    public void Configure(EntityTypeBuilder<ReservationRuleOverride> b)
    {
        // CK_RULE_OVERRIDE_GRADE migration'da ham SQL ile eklenir (bkz. OracleCheckConstraints).
        b.ToTable("RESERVATION_RULE_OVERRIDE");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("ID").HasMaxLength(Ids.Length).ValueGeneratedNever();
        b.Property(x => x.RuleKey).HasColumnName("RULE_KEY").HasMaxLength(50).IsRequired();
        b.Property(x => x.Grade).HasColumnName("GRADE").HasMaxLength(30).IsRequired();
        b.Property(x => x.BuildingId).HasColumnName("BUILDING_ID").HasMaxLength(Ids.Length);
        b.Property(x => x.Enabled).HasColumnName("ENABLED");
        b.Property(x => x.IntValue).HasColumnName("INT_VALUE");
        b.Property(x => x.UpdatedAt).HasColumnName("UPDATED_AT").IsRequired();
        b.Property(x => x.UpdatedById).HasColumnName("UPDATED_BY_ID").HasMaxLength(Ids.Length);
        b.HasOne(x => x.Building).WithMany().HasForeignKey(x => x.BuildingId).OnDelete(DeleteBehavior.Cascade);
        // Oracle bilesik unique'te NULL olmayan kisimlari ayni olan satirlari da reddeder.
        b.HasIndex(x => new { x.RuleKey, x.Grade, x.BuildingId }).IsUnique().HasDatabaseName("UX_RULE_OVERRIDE_SCOPE");
    }
}

public sealed class HolidayConfiguration : IEntityTypeConfiguration<Holiday>
{
    public void Configure(EntityTypeBuilder<Holiday> b)
    {
        b.ToTable("HOLIDAY");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("ID").HasMaxLength(Ids.Length).ValueGeneratedNever();
        b.Property(x => x.Date).HasColumnName("HOLIDAY_DATE").IsRequired();
        b.Property(x => x.Name).HasColumnName("NAME").HasMaxLength(80).IsRequired();
        b.Property(x => x.CreatedAt).HasColumnName("CREATED_AT").IsRequired();
        b.Property(x => x.CreatedById).HasColumnName("CREATED_BY_ID").HasMaxLength(Ids.Length);
        b.Ignore(x => x.DayKey);
        b.HasIndex(x => x.Date).IsUnique().HasDatabaseName("UX_HOLIDAY_DATE");
    }
}

public sealed class CapacityShortfallConfiguration : IEntityTypeConfiguration<CapacityShortfall>
{
    public void Configure(EntityTypeBuilder<CapacityShortfall> b)
    {
        b.ToTable("CAPACITY_SHORTFALL");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("ID").HasMaxLength(Ids.Length).ValueGeneratedNever();
        b.Property(x => x.Date).HasColumnName("SHORTFALL_DATE").IsRequired();
        b.Property(x => x.Requests).HasColumnName("REQUESTS").IsRequired();
        b.Property(x => x.Fulfilled).HasColumnName("FULFILLED").IsRequired();
        b.Property(x => x.Unmet).HasColumnName("UNMET").IsRequired();
        b.Property(x => x.Source).HasColumnName("SOURCE").HasMaxLength(20).IsRequired().HasDefaultValue(ShortfallSources.System);
        b.Property(x => x.CreatedAt).HasColumnName("CREATED_AT").IsRequired();
        b.Property(x => x.UpdatedAt).HasColumnName("UPDATED_AT").IsRequired();
        // Kaynakta hem @unique hem @@index([date]) vardi; unique index ikisini de karsilar.
        b.HasIndex(x => x.Date).IsUnique().HasDatabaseName("UX_CAPACITY_SHORTFALL_DATE");
    }
}

public sealed class ClosureConfiguration : IEntityTypeConfiguration<Closure>
{
    public void Configure(EntityTypeBuilder<Closure> b)
    {
        b.ToTable("CLOSURE");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("ID").HasMaxLength(Ids.Length).ValueGeneratedNever();
        b.Property(x => x.Scope).HasColumnName("SCOPE").HasMaxLength(20).IsRequired();
        b.Property(x => x.FloorId).HasColumnName("FLOOR_ID").HasMaxLength(Ids.Length);
        b.Property(x => x.DeskId).HasColumnName("DESK_ID").HasMaxLength(Ids.Length);
        b.Property(x => x.RoomId).HasColumnName("ROOM_ID").HasMaxLength(Ids.Length);
        b.Property(x => x.StartDate).HasColumnName("START_DATE").IsRequired();
        b.Property(x => x.EndDate).HasColumnName("END_DATE").IsRequired();
        b.Property(x => x.Reason).HasColumnName("REASON").HasMaxLength(20).IsRequired();
        b.Property(x => x.Note).HasColumnName("NOTE").HasMaxLength(240).IsRequired();
        b.Property(x => x.Active).HasColumnName("ACTIVE").IsRequired().HasDefaultValue(true);
        b.Property(x => x.CreatedById).HasColumnName("CREATED_BY_ID").HasMaxLength(Ids.Length).IsRequired();
        b.Property(x => x.CreatedAt).HasColumnName("CREATED_AT").IsRequired();
        b.Property(x => x.UpdatedAt).HasColumnName("UPDATED_AT").IsRequired();

        b.HasOne(x => x.Floor).WithMany(x => x.Closures).HasForeignKey(x => x.FloorId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Desk).WithMany(x => x.Closures).HasForeignKey(x => x.DeskId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Room).WithMany(x => x.Closures).HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => new { x.StartDate, x.EndDate }).HasDatabaseName("IX_CLOSURE_DATES");
        b.HasIndex(x => x.Scope).HasDatabaseName("IX_CLOSURE_SCOPE");
    }
}

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.ToTable("NOTIFICATION");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("ID").HasMaxLength(Ids.Length).ValueGeneratedNever();
        b.Property(x => x.UserId).HasColumnName("USER_ID").HasMaxLength(Ids.Length).IsRequired();
        b.Property(x => x.Type).HasColumnName("NOTIFICATION_TYPE").HasMaxLength(50).IsRequired();
        b.Property(x => x.Title).HasColumnName("TITLE").HasMaxLength(200).IsRequired();
        b.Property(x => x.Body).HasColumnName("BODY").HasMaxLength(1000).IsRequired();
        b.Property(x => x.Link).HasColumnName("LINK").HasMaxLength(400);
        b.Property(x => x.Severity).HasColumnName("SEVERITY").HasMaxLength(20).IsRequired().HasDefaultValue(NotificationSeverities.Info);
        b.Property(x => x.ReadAt).HasColumnName("READ_AT");
        b.Property(x => x.CreatedAt).HasColumnName("CREATED_AT").IsRequired();

        b.HasOne(x => x.User).WithMany(x => x.Notifications).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.UserId, x.ReadAt }).HasDatabaseName("IX_NOTIFICATION_USER_READ");
        b.HasIndex(x => x.CreatedAt).HasDatabaseName("IX_NOTIFICATION_CREATED");
    }
}

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.ToTable("AUDIT_LOG");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("ID").HasMaxLength(Ids.Length).ValueGeneratedNever();
        b.Property(x => x.ActorId).HasColumnName("ACTOR_ID").HasMaxLength(Ids.Length);
        b.Property(x => x.ActorName).HasColumnName("ACTOR_NAME").HasMaxLength(200).IsRequired();
        b.Property(x => x.ActorRole).HasColumnName("ACTOR_ROLE").HasMaxLength(20).IsRequired();
        b.Property(x => x.Action).HasColumnName("ACTION").HasMaxLength(50).IsRequired();
        b.Property(x => x.EntityType).HasColumnName("ENTITY_TYPE").HasMaxLength(50).IsRequired();
        b.Property(x => x.EntityId).HasColumnName("ENTITY_ID").HasMaxLength(64);
        b.Property(x => x.Summary).HasColumnName("SUMMARY").HasMaxLength(1000).IsRequired();
        b.Property(x => x.Metadata).HasColumnName("METADATA").HasColumnType("CLOB");
        b.Property(x => x.CreatedAt).HasColumnName("CREATED_AT").IsRequired();

        b.HasIndex(x => x.CreatedAt).HasDatabaseName("IX_AUDIT_LOG_CREATED");
        b.HasIndex(x => x.EntityType).HasDatabaseName("IX_AUDIT_LOG_ENTITY_TYPE");
        b.HasIndex(x => x.ActorId).HasDatabaseName("IX_AUDIT_LOG_ACTOR");
    }
}
