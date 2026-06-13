using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Valkompass.Domain.Entities;

namespace Valkompass.Infrastructure.Persistence.Configurations;

public class PollConfiguration : IEntityTypeConfiguration<Poll>
{
    public void Configure(EntityTypeBuilder<Poll> b)
    {
        b.HasKey(p => p.Id);
        b.Property(p => p.ExternalKey).HasMaxLength(96).IsRequired();
        b.HasIndex(p => p.ExternalKey).IsUnique();
        b.Property(p => p.SourceCitation).HasMaxLength(512);
        b.Property(p => p.SourceUrl).HasMaxLength(1024);

        b.HasOne(p => p.Pollster)
            .WithMany(x => x.Polls)
            .HasForeignKey(p => p.PollsterId)
            .OnDelete(DeleteBehavior.Cascade);

        // Snabb sortering/filtrering på tidsaxeln.
        b.HasIndex(p => p.PublishedAt);
    }
}
