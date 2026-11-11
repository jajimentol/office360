using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ofis360.Domain.Common;
using Ofis360.Domain.Entities;

namespace Ofis360.Infrastructure.Persistence.Configurations;

public sealed class AppSessionConfiguration : IEntityTypeConfiguration<AppSession>
{
    public void Configure(EntityTypeBuilder<AppSession> b)
    {
        b.ToTable("APP_SESSION");
        b.HasKey(x => x.TokenHash);
        b.Property(x => x.TokenHash).HasColumnName("TOKEN_HASH").HasMaxLength(64).ValueGeneratedNever();
        b.Property(x => x.UserId).HasColumnName("USER_ID").HasMaxLength(Ids.Length).IsRequired();
        b.Property(x => x.CreatedAt).HasColumnName("CREATED_AT").IsRequired();
        b.Property(x => x.LastSeenAt).HasColumnName("LAST_SEEN_AT").IsRequired();
        b.Property(x => x.ExpiresAt).HasColumnName("EXPIRES_AT").IsRequired();
        b.Property(x => x.RevokedAt).HasColumnName("REVOKED_AT");
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => x.UserId).HasDatabaseName("IX_APP_SESSION_USER");
        b.HasIndex(x => x.ExpiresAt).HasDatabaseName("IX_APP_SESSION_EXPIRES");
    }
}

/// <summary>Kaynak: Prisma User. Oracle'da USER ayrilmis sozcuk oldugu icin tablo adi APP_USER.</summary>
public sealed class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> b)
    {
        // CK_APP_USER_ROLE CHECK kisiti migration'da ham SQL ile eklenir (bkz. OracleCheckConstraints):
        // Oracle EF saglayicisi CREATE TABLE'i EXECUTE IMMEDIATE icine sarar ve icerideki tek tirnaklari kacirmaz.
        b.ToTable("APP_USER");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("ID").HasMaxLength(Ids.Length).ValueGeneratedNever();
        b.Property(x => x.Email).HasColumnName("EMAIL").HasMaxLength(320).IsRequired();
        b.Property(x => x.FullName).HasColumnName("FULL_NAME").HasMaxLength(200).IsRequired();
        b.Property(x => x.Title).HasColumnName("TITLE").HasMaxLength(200).IsRequired();
        b.Property(x => x.Role).HasColumnName("ROLE").HasMaxLength(20).IsRequired().HasDefaultValue(Roles.Employee);
        b.Property(x => x.DepartmentId).HasColumnName("DEPARTMENT_ID").HasMaxLength(Ids.Length).IsRequired();
        b.Property(x => x.Grade).HasColumnName("GRADE").HasMaxLength(30);
        b.Property(x => x.IsSecretary).HasColumnName("IS_SECRETARY").IsRequired().HasDefaultValue(false);
        b.Property(x => x.AccessibilityApproved).HasColumnName("ACCESSIBILITY_APPROVED").IsRequired().HasDefaultValue(false);
        b.Property(x => x.IsDemoAccount).HasColumnName("IS_DEMO_ACCOUNT").IsRequired().HasDefaultValue(false);
        b.Property(x => x.Active).HasColumnName("ACTIVE").IsRequired().HasDefaultValue(true);
        b.Property(x => x.PreferredFloorId).HasColumnName("PREFERRED_FLOOR_ID").HasMaxLength(Ids.Length);
        b.Property(x => x.PrefersQuietZone).HasColumnName("PREFERS_QUIET_ZONE").IsRequired().HasDefaultValue(false);
        b.Property(x => x.NotificationsEnabled).HasColumnName("NOTIFICATIONS_ENABLED").IsRequired().HasDefaultValue(true);
        b.Property(x => x.CreatedAt).HasColumnName("CREATED_AT").IsRequired();
        b.Property(x => x.UpdatedAt).HasColumnName("UPDATED_AT").IsRequired();

        b.HasOne(x => x.Department).WithMany(d => d.Users).HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.Email).IsUnique().HasDatabaseName("UX_APP_USER_EMAIL");
        b.HasIndex(x => x.Role).HasDatabaseName("IX_APP_USER_ROLE");
        b.HasIndex(x => x.DepartmentId).HasDatabaseName("IX_APP_USER_DEPARTMENT");
        b.HasIndex(x => new { x.Active, x.FullName }).HasDatabaseName("IX_APP_USER_ACTIVE_NAME");
    }
}
