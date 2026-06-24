using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Valkompass.Domain.Entities;

namespace Valkompass.Infrastructure.Persistence.Configurations;

public class QuizEventConfiguration : IEntityTypeConfiguration<QuizEvent>
{
    public void Configure(EntityTypeBuilder<QuizEvent> b)
    {
        b.HasKey(e => e.Id);
        b.Property(e => e.Type).HasConversion<int>();
        b.Property(e => e.Variant).HasConversion<int>();

        // Aggregaten grupperar på typ + tid; indexet stödjer både funnel-räkning och tidsserier.
        b.HasIndex(e => new { e.Type, e.OccurredAt });
    }
}
