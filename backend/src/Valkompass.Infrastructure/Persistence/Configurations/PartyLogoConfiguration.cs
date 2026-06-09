using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Valkompass.Domain.Entities;

namespace Valkompass.Infrastructure.Persistence.Configurations;

public class PartyLogoConfiguration : IEntityTypeConfiguration<PartyLogo>
{
    public void Configure(EntityTypeBuilder<PartyLogo> b)
    {
        b.HasKey(l => l.PartyId);
        b.Property(l => l.Data).IsRequired();
        b.Property(l => l.ContentType).HasMaxLength(64).IsRequired();
        b.HasOne(l => l.Party)
            .WithOne()
            .HasForeignKey<PartyLogo>(l => l.PartyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
