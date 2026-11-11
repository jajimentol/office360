using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ofis360.Domain.Common;
using Ofis360.Domain.Entities;

namespace Ofis360.Infrastructure.Persistence.Configurations;

public sealed class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> b)
    {
        b.ToTable("DEPARTMENT");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("ID").HasMaxLength(Ids.Length).ValueGeneratedNever();
        b.Property(x => x.Code).HasColumnName("CODE").HasMaxLength(40).IsRequired();
        b.Property(x => x.Name).HasColumnName("NAME").HasMaxLength(200).IsRequired();
        b.Property(x => x.Color).HasColumnName("COLOR").HasMaxLength(20).IsRequired().HasDefaultValue("#0f4c81");
        b.Property(x => x.Active).HasColumnName("ACTIVE").IsRequired().HasDefaultValue(true);
        b.Property(x => x.CreatedAt).HasColumnName("CREATED_AT").IsRequired();
        b.HasIndex(x => x.Code).IsUnique().HasDatabaseName("UX_DEPARTMENT_CODE");
    }
}
