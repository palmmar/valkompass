using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Valkompass.Domain.Entities;

namespace Valkompass.Infrastructure.Persistence.Configurations;

public class ElectionSnapshotRecordConfiguration : IEntityTypeConfiguration<ElectionSnapshotRecord>
{
    public void Configure(EntityTypeBuilder<ElectionSnapshotRecord> b)
    {
        b.HasKey(e => e.Id);
        b.Property(e => e.Stage).HasConversion<int>();
        b.Property(e => e.Checksum).HasMaxLength(64).IsRequired();
        b.Property(e => e.Payload).HasColumnType("jsonb").IsRequired();
        b.Property(e => e.Forecast).HasColumnType("jsonb");

        // Samma checksumma per räkningstillfälle importeras bara en gång. Gör importen
        // idempotent även om två försök skulle råka överlappa.
        b.HasIndex(e => new { e.Stage, e.Checksum }).IsUnique();

        // "Senaste snapshot" är den vanligaste frågan och ställs vid varje API-anrop.
        b.HasIndex(e => new { e.Stage, e.IngestedAt });
    }
}
