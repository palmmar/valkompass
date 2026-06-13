using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Valkompass.Domain.Entities;

namespace Valkompass.Infrastructure.Persistence.Configurations;

public class PollsterConfiguration : IEntityTypeConfiguration<Pollster>
{
    public void Configure(EntityTypeBuilder<Pollster> b)
    {
        b.HasKey(p => p.Id);
        b.Property(p => p.Code).HasMaxLength(32).IsRequired();
        b.HasIndex(p => p.Code).IsUnique();
        b.Property(p => p.DisplayName).HasMaxLength(128).IsRequired();
        b.Property(p => p.Method).HasMaxLength(64);
        b.Property(p => p.Commissioner).HasMaxLength(128);
    }
}
