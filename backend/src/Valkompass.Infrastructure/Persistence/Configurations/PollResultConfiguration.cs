using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Valkompass.Domain.Entities;

namespace Valkompass.Infrastructure.Persistence.Configurations;

public class PollResultConfiguration : IEntityTypeConfiguration<PollResult>
{
    public void Configure(EntityTypeBuilder<PollResult> b)
    {
        b.HasKey(p => p.Id);

        // Value är nullable på databasnivå: null = under redovisningsgräns ("redovisas ej"),
        // aldrig 0 (samma invariant som PartyPosition.Value).
        b.Property(p => p.Value);

        b.HasOne(p => p.Poll)
            .WithMany(x => x.Results)
            .HasForeignKey(p => p.PollId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(p => p.Party)
            .WithMany()
            .HasForeignKey(p => p.PartyId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(p => new { p.PollId, p.PartyId }).IsUnique();
    }
}
