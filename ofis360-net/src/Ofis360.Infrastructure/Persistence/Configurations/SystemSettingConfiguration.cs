using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ofis360.Domain.Common;
using Ofis360.Domain.Entities;

namespace Ofis360.Infrastructure.Persistence.Configurations;

/// <summary>GROUP, TYPE, KEY, VALUE, ORDER Oracle'da ayrilmis sozcuk; sutunlar SETTING_* / SORT_ORDER olarak adlandirildi.</summary>
public sealed class SystemSettingConfiguration : IEntityTypeConfiguration<SystemSetting>
{
    public void Configure(EntityTypeBuilder<SystemSetting> b)
    {
        // CK_SYSTEM_SETTING_TYPE CHECK kisiti migration'da ham SQL ile eklenir (bkz. OracleCheckConstraints).
        b.ToTable("SYSTEM_SETTING");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("ID").HasMaxLength(Ids.Length).ValueGeneratedNever();
        b.Property(x => x.Key).HasColumnName("SETTING_KEY").HasMaxLength(100).IsRequired();
        b.Property(x => x.Value).HasColumnName("SETTING_VALUE").HasMaxLength(2000).IsRequired(false).HasConversion<EmptyStringConverter>();
        b.Property(x => x.Label).HasColumnName("LABEL").HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasColumnName("DESCRIPTION").HasMaxLength(1000).IsRequired(false).HasConversion<EmptyStringConverter>();
        b.Property(x => x.Type).HasColumnName("SETTING_TYPE").HasMaxLength(20).IsRequired().HasDefaultValue(SettingTypes.Text);
        b.Property(x => x.Group).HasColumnName("SETTING_GROUP").HasMaxLength(50).IsRequired().HasDefaultValue("GENERAL");
        b.Property(x => x.Order).HasColumnName("SORT_ORDER").IsRequired().HasDefaultValue(0);
        b.Property(x => x.UpdatedAt).HasColumnName("UPDATED_AT").IsRequired();
        b.HasIndex(x => x.Key).IsUnique().HasDatabaseName("UX_SYSTEM_SETTING_KEY");
    }
}
