using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Valkompass.Domain.Entities;

namespace Valkompass.Infrastructure.Persistence.Configurations;

public class PartyConfiguration : IEntityTypeConfiguration<Party>
{
    public void Configure(EntityTypeBuilder<Party> b)
    {
        b.HasKey(p => p.Id);
        b.Property(p => p.Code).HasMaxLength(8).IsRequired();
        b.HasIndex(p => p.Code).IsUnique();
        b.Property(p => p.Name).HasMaxLength(128).IsRequired();
        b.Property(p => p.FullName).HasMaxLength(256).IsRequired();
        b.Property(p => p.ShortDescription).HasMaxLength(1024);
        b.Property(p => p.Color).HasMaxLength(16);
        b.Property(p => p.LogoUrl).HasMaxLength(512);
    }
}
