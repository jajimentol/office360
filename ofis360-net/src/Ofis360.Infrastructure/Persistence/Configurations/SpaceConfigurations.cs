using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ofis360.Domain.Common;
using Ofis360.Domain.Entities;

namespace Ofis360.Infrastructure.Persistence.Configurations;

// Oracle ayrilmis sozcukler: ORDER → SORT_ORDER, NUMBER → FLOOR_NUMBER, KEY → FEATURE_KEY.
// Deger listeli CHECK kisitlari migration'da ham SQL ile eklenir (OracleCheckConstraints).

public sealed class BuildingConfiguration : IEntityTypeConfiguration<Building>
{
    public void Configure(EntityTypeBuilder<Building> b)
    {
        b.ToTable("BUILDING");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("ID").HasMaxLength(Ids.Length).ValueGeneratedNever();
        b.Property(x => x.Code).HasColumnName("CODE").HasMaxLength(12).IsRequired();
        b.Property(x => x.Name).HasColumnName("NAME").HasMaxLength(80).IsRequired();
        b.Property(x => x.Address).HasColumnName("ADDRESS").HasMaxLength(240).IsRequired(false).HasConversion<EmptyStringConverter>();
        b.Property(x => x.Order).HasColumnName("SORT_ORDER").IsRequired().HasDefaultValue(0);
        b.Property(x => x.Active).HasColumnName("ACTIVE").IsRequired().HasDefaultValue(true);
        b.Property(x => x.IsDemo).HasColumnName("IS_DEMO").IsRequired().HasDefaultValue(false);
        b.Property(x => x.CreatedAt).HasColumnName("CREATED_AT").IsRequired();
        b.Property(x => x.UpdatedAt).HasColumnName("UPDATED_AT").IsRequired();
        b.HasIndex(x => x.Code).IsUnique().HasDatabaseName("UX_BUILDING_CODE");
    }
}

public sealed class FloorConfiguration : IEntityTypeConfiguration<Floor>
{
    public void Configure(EntityTypeBuilder<Floor> b)
    {
        b.ToTable("FLOOR");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("ID").HasMaxLength(Ids.Length).ValueGeneratedNever();
        b.Property(x => x.BuildingId).HasColumnName("BUILDING_ID").HasMaxLength(Ids.Length).IsRequired();
        b.Property(x => x.Number).HasColumnName("FLOOR_NUMBER").IsRequired();
        b.Property(x => x.Name).HasColumnName("NAME").HasMaxLength(60).IsRequired();
        b.Property(x => x.Description).HasColumnName("DESCRIPTION").HasMaxLength(240).IsRequired(false).HasConversion<EmptyStringConverter>();
        b.Property(x => x.Reservable).HasColumnName("RESERVABLE").IsRequired().HasDefaultValue(true);
        b.Property(x => x.Active).HasColumnName("ACTIVE").IsRequired().HasDefaultValue(true);
        b.Property(x => x.PlanWidth).HasColumnName("PLAN_WIDTH").IsRequired().HasDefaultValue(1000);
        b.Property(x => x.PlanHeight).HasColumnName("PLAN_HEIGHT").IsRequired().HasDefaultValue(640);
        b.Property(x => x.CreatedAt).HasColumnName("CREATED_AT").IsRequired();
        b.Property(x => x.UpdatedAt).HasColumnName("UPDATED_AT").IsRequired();

        b.HasOne(x => x.Building).WithMany(x => x.Floors).HasForeignKey(x => x.BuildingId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.BuildingId, x.Number }).IsUnique().HasDatabaseName("UX_FLOOR_BUILDING_NUMBER");
        b.HasIndex(x => new { x.BuildingId, x.Active }).HasDatabaseName("IX_FLOOR_BUILDING_ACTIVE");
    }
}

public sealed class ZoneConfiguration : IEntityTypeConfiguration<Zone>
{
    public void Configure(EntityTypeBuilder<Zone> b)
    {
        b.ToTable("ZONE");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("ID").HasMaxLength(Ids.Length).ValueGeneratedNever();
        b.Property(x => x.FloorId).HasColumnName("FLOOR_ID").HasMaxLength(Ids.Length).IsRequired();
        b.Property(x => x.Code).HasColumnName("CODE").HasMaxLength(40).IsRequired();
        b.Property(x => x.Name).HasColumnName("NAME").HasMaxLength(60).IsRequired();
        b.Property(x => x.Kind).HasColumnName("KIND").HasMaxLength(20).IsRequired().HasDefaultValue(ZoneKinds.Work);
        b.Property(x => x.X).HasColumnName("X").IsRequired();
        b.Property(x => x.Y).HasColumnName("Y").IsRequired();
        b.Property(x => x.Width).HasColumnName("WIDTH").IsRequired();
        b.Property(x => x.Height).HasColumnName("HEIGHT").IsRequired();
        b.Property(x => x.WindowSide).HasColumnName("WINDOW_SIDE").IsRequired().HasDefaultValue(false);
        b.Property(x => x.Quiet).HasColumnName("QUIET").IsRequired().HasDefaultValue(false);
        b.Property(x => x.Reservable).HasColumnName("RESERVABLE").IsRequired().HasDefaultValue(true);

        b.HasOne(x => x.Floor).WithMany(x => x.Zones).HasForeignKey(x => x.FloorId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.FloorId, x.Code }).IsUnique().HasDatabaseName("UX_ZONE_FLOOR_CODE");
    }
}

public sealed class DeskFeatureConfiguration : IEntityTypeConfiguration<DeskFeature>
{
    public void Configure(EntityTypeBuilder<DeskFeature> b)
    {
        b.ToTable("DESK_FEATURE");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("ID").HasMaxLength(Ids.Length).ValueGeneratedNever();
        b.Property(x => x.Key).HasColumnName("FEATURE_KEY").HasMaxLength(40).IsRequired();
        b.Property(x => x.Name).HasColumnName("NAME").HasMaxLength(80).IsRequired();
        b.Property(x => x.Description).HasColumnName("DESCRIPTION").HasMaxLength(240).IsRequired(false).HasConversion<EmptyStringConverter>();
        b.Property(x => x.Icon).HasColumnName("ICON").HasMaxLength(40).IsRequired();
        b.HasIndex(x => x.Key).IsUnique().HasDatabaseName("UX_DESK_FEATURE_KEY");
    }
}

public sealed class DeskConfiguration : IEntityTypeConfiguration<Desk>
{
    public void Configure(EntityTypeBuilder<Desk> b)
    {
        b.ToTable("DESK");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("ID").HasMaxLength(Ids.Length).ValueGeneratedNever();
        b.Property(x => x.Code).HasColumnName("CODE").HasMaxLength(20).IsRequired();
        b.Property(x => x.FloorId).HasColumnName("FLOOR_ID").HasMaxLength(Ids.Length).IsRequired();
        b.Property(x => x.ZoneId).HasColumnName("ZONE_ID").HasMaxLength(Ids.Length).IsRequired();
        b.Property(x => x.X).HasColumnName("X").IsRequired();
        b.Property(x => x.Y).HasColumnName("Y").IsRequired();
        b.Property(x => x.Width).HasColumnName("WIDTH").IsRequired().HasDefaultValue(46);
        b.Property(x => x.Height).HasColumnName("HEIGHT").IsRequired().HasDefaultValue(34);
        b.Property(x => x.Rotation).HasColumnName("ROTATION").IsRequired().HasDefaultValue(0);
        b.Property(x => x.Placed).HasColumnName("PLACED").IsRequired().HasDefaultValue(true);
        b.Property(x => x.Active).HasColumnName("ACTIVE").IsRequired().HasDefaultValue(true);
        b.Property(x => x.AccessibleOnly).HasColumnName("ACCESSIBLE_ONLY").IsRequired().HasDefaultValue(false);
        b.Property(x => x.Popular).HasColumnName("POPULAR").IsRequired().HasDefaultValue(false);
        b.Property(x => x.Notes).HasColumnName("NOTES").HasMaxLength(240);
        b.Property(x => x.CreatedAt).HasColumnName("CREATED_AT").IsRequired();
        b.Property(x => x.UpdatedAt).HasColumnName("UPDATED_AT").IsRequired();

        b.HasOne(x => x.Floor).WithMany(x => x.Desks).HasForeignKey(x => x.FloorId).OnDelete(DeleteBehavior.Cascade);
        // Zone → Desk da cascade; Oracle ayni tabloya iki cascade yolunu kabul eder (SQL Server'daki kisit yok).
        b.HasOne(x => x.Zone).WithMany(x => x.Desks).HasForeignKey(x => x.ZoneId).OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.Code).IsUnique().HasDatabaseName("UX_DESK_CODE");
        b.HasIndex(x => x.FloorId).HasDatabaseName("IX_DESK_FLOOR");
        b.HasIndex(x => new { x.FloorId, x.Placed }).HasDatabaseName("IX_DESK_FLOOR_PLACED");
        b.HasIndex(x => x.ZoneId).HasDatabaseName("IX_DESK_ZONE");
    }
}

public sealed class DeskFeatureOnDeskConfiguration : IEntityTypeConfiguration<DeskFeatureOnDesk>
{
    public void Configure(EntityTypeBuilder<DeskFeatureOnDesk> b)
    {
        b.ToTable("DESK_FEATURE_LINK");
        b.HasKey(x => new { x.DeskId, x.FeatureId });
        b.Property(x => x.DeskId).HasColumnName("DESK_ID").HasMaxLength(Ids.Length);
        b.Property(x => x.FeatureId).HasColumnName("FEATURE_ID").HasMaxLength(Ids.Length);
        b.HasOne(x => x.Desk).WithMany(x => x.Features).HasForeignKey(x => x.DeskId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Feature).WithMany(x => x.Desks).HasForeignKey(x => x.FeatureId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class RoomFeatureConfiguration : IEntityTypeConfiguration<RoomFeature>
{
    public void Configure(EntityTypeBuilder<RoomFeature> b)
    {
        b.ToTable("ROOM_FEATURE");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("ID").HasMaxLength(Ids.Length).ValueGeneratedNever();
        b.Property(x => x.Key).HasColumnName("FEATURE_KEY").HasMaxLength(40).IsRequired();
        b.Property(x => x.Name).HasColumnName("NAME").HasMaxLength(80).IsRequired();
        b.Property(x => x.Icon).HasColumnName("ICON").HasMaxLength(40).IsRequired();
        b.HasIndex(x => x.Key).IsUnique().HasDatabaseName("UX_ROOM_FEATURE_KEY");
    }
}

public sealed class MeetingRoomConfiguration : IEntityTypeConfiguration<MeetingRoom>
{
    public void Configure(EntityTypeBuilder<MeetingRoom> b)
    {
        b.ToTable("MEETING_ROOM");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("ID").HasMaxLength(Ids.Length).ValueGeneratedNever();
        b.Property(x => x.Code).HasColumnName("CODE").HasMaxLength(20).IsRequired();
        b.Property(x => x.Name).HasColumnName("NAME").HasMaxLength(60).IsRequired();
        b.Property(x => x.FloorId).HasColumnName("FLOOR_ID").HasMaxLength(Ids.Length).IsRequired();
        b.Property(x => x.Capacity).HasColumnName("CAPACITY").IsRequired();
        b.Property(x => x.X).HasColumnName("X").IsRequired();
        b.Property(x => x.Y).HasColumnName("Y").IsRequired();
        b.Property(x => x.Width).HasColumnName("WIDTH").IsRequired();
        b.Property(x => x.Height).HasColumnName("HEIGHT").IsRequired();
        b.Property(x => x.Placed).HasColumnName("PLACED").IsRequired().HasDefaultValue(true);
        b.Property(x => x.Active).HasColumnName("ACTIVE").IsRequired().HasDefaultValue(true);
        b.Property(x => x.Notes).HasColumnName("NOTES").HasMaxLength(240);
        b.Property(x => x.SecretaryOnly).HasColumnName("SECRETARY_ONLY").IsRequired().HasDefaultValue(false);
        b.Property(x => x.PriorityOpenDays).HasColumnName("PRIORITY_OPEN_DAYS");
        b.Property(x => x.CreatedAt).HasColumnName("CREATED_AT").IsRequired();
        b.Property(x => x.UpdatedAt).HasColumnName("UPDATED_AT").IsRequired();

        b.HasOne(x => x.Floor).WithMany(x => x.Rooms).HasForeignKey(x => x.FloorId).OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.Code).IsUnique().HasDatabaseName("UX_MEETING_ROOM_CODE");
        b.HasIndex(x => x.FloorId).HasDatabaseName("IX_MEETING_ROOM_FLOOR");
        b.HasIndex(x => new { x.FloorId, x.Placed }).HasDatabaseName("IX_MEETING_ROOM_FLOOR_PLACED");
    }
}

public sealed class RoomAccessRuleConfiguration : IEntityTypeConfiguration<RoomAccessRule>
{
    public void Configure(EntityTypeBuilder<RoomAccessRule> b)
    {
        // CK_ROOM_ACCESS_GRADE migration'da ham SQL ile eklenir (bkz. OracleCheckConstraints).
        b.ToTable("ROOM_ACCESS_RULE");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("ID").HasMaxLength(Ids.Length).ValueGeneratedNever();
        b.Property(x => x.RoomId).HasColumnName("ROOM_ID").HasMaxLength(Ids.Length).IsRequired();
        b.Property(x => x.Grade).HasColumnName("GRADE").HasMaxLength(30).IsRequired();
        b.Property(x => x.MinAttendees).HasColumnName("MIN_ATTENDEES").IsRequired();
        b.Property(x => x.BypassPriority).HasColumnName("BYPASS_PRIORITY").IsRequired().HasDefaultValue(false);
        b.HasOne(x => x.Room).WithMany(x => x.AccessRules).HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.RoomId, x.Grade }).IsUnique().HasDatabaseName("UX_ROOM_ACCESS_RULE");
    }
}

public sealed class RoomPriorityUserConfiguration : IEntityTypeConfiguration<RoomPriorityUser>
{
    public void Configure(EntityTypeBuilder<RoomPriorityUser> b)
    {
        b.ToTable("ROOM_PRIORITY_USER");
        b.HasKey(x => new { x.RoomId, x.UserId });
        b.Property(x => x.RoomId).HasColumnName("ROOM_ID").HasMaxLength(Ids.Length);
        b.Property(x => x.UserId).HasColumnName("USER_ID").HasMaxLength(Ids.Length);
        b.HasOne(x => x.Room).WithMany(x => x.PriorityUsers).HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => x.UserId).HasDatabaseName("IX_ROOM_PRIORITY_USER");
    }
}

public sealed class RoomFeatureOnRoomConfiguration : IEntityTypeConfiguration<RoomFeatureOnRoom>
{
    public void Configure(EntityTypeBuilder<RoomFeatureOnRoom> b)
    {
        b.ToTable("ROOM_FEATURE_LINK");
        b.HasKey(x => new { x.RoomId, x.FeatureId });
        b.Property(x => x.RoomId).HasColumnName("ROOM_ID").HasMaxLength(Ids.Length);
        b.Property(x => x.FeatureId).HasColumnName("FEATURE_ID").HasMaxLength(Ids.Length);
        b.HasOne(x => x.Room).WithMany(x => x.Features).HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Feature).WithMany(x => x.Rooms).HasForeignKey(x => x.FeatureId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class FloorPlanElementConfiguration : IEntityTypeConfiguration<FloorPlanElement>
{
    public void Configure(EntityTypeBuilder<FloorPlanElement> b)
    {
        b.ToTable("FLOOR_PLAN_ELEMENT");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("ID").HasMaxLength(Ids.Length).ValueGeneratedNever();
        b.Property(x => x.FloorId).HasColumnName("FLOOR_ID").HasMaxLength(Ids.Length).IsRequired();
        b.Property(x => x.Type).HasColumnName("ELEMENT_TYPE").HasMaxLength(20).IsRequired();
        b.Property(x => x.Label).HasColumnName("LABEL").HasMaxLength(80);
        b.Property(x => x.X).HasColumnName("X").IsRequired();
        b.Property(x => x.Y).HasColumnName("Y").IsRequired();
        b.Property(x => x.Width).HasColumnName("WIDTH").IsRequired();
        b.Property(x => x.Height).HasColumnName("HEIGHT").IsRequired();
        b.Property(x => x.Rotation).HasColumnName("ROTATION").IsRequired().HasDefaultValue(0);
        b.Property(x => x.Style).HasColumnName("STYLE").HasMaxLength(400).IsRequired().HasDefaultValue("{}");
        b.Property(x => x.LayerOrder).HasColumnName("LAYER_ORDER").IsRequired().HasDefaultValue(0);
        b.Property(x => x.Locked).HasColumnName("LOCKED").IsRequired().HasDefaultValue(false);
        b.Property(x => x.Visible).HasColumnName("VISIBLE").IsRequired().HasDefaultValue(true);
        b.Property(x => x.LinkedResourceId).HasColumnName("LINKED_RESOURCE_ID").HasMaxLength(64);
        b.Property(x => x.LinkedResourceType).HasColumnName("LINKED_RESOURCE_TYPE").HasMaxLength(20);
        b.Property(x => x.CreatedAt).HasColumnName("CREATED_AT").IsRequired();
        b.Property(x => x.UpdatedAt).HasColumnName("UPDATED_AT").IsRequired();

        b.HasOne(x => x.Floor).WithMany(x => x.Elements).HasForeignKey(x => x.FloorId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.FloorId, x.LayerOrder }).HasDatabaseName("IX_FLOOR_PLAN_ELEMENT_FLOOR_LAYER");
    }
}
