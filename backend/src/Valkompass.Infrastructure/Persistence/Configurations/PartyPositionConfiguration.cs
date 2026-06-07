using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Valkompass.Domain.Entities;

namespace Valkompass.Infrastructure.Persistence.Configurations;

public class PartyPositionConfiguration : IEntityTypeConfiguration<PartyPosition>
{
    public void Configure(EntityTypeBuilder<PartyPosition> b)
    {
        b.HasKey(p => p.Id);

        // Nullable ScaleValue lagras som nullable int (null = oklar position).
        b.Property(p => p.Value).HasConversion<int?>();

        b.Property(p => p.Motivation).HasMaxLength(4000);
        b.Property(p => p.SourceCitation).HasMaxLength(512);
        b.Property(p => p.SourceUrl).HasMaxLength(1024);

        b.HasOne(p => p.Party)
            .WithMany(x => x.Positions)
            .HasForeignKey(p => p.PartyId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(p => p.Question)
            .WithMany(x => x.Positions)
            .HasForeignKey(p => p.QuestionId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(p => new { p.PartyId, p.QuestionId }).IsUnique();
    }
}
