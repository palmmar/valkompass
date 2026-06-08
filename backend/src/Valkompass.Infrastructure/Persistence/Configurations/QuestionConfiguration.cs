using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Valkompass.Domain.Entities;

namespace Valkompass.Infrastructure.Persistence.Configurations;

public class QuestionConfiguration : IEntityTypeConfiguration<Question>
{
    public void Configure(EntityTypeBuilder<Question> b)
    {
        b.HasKey(q => q.Id);
        b.Property(q => q.ExternalKey).HasMaxLength(128).IsRequired();
        b.HasIndex(q => q.ExternalKey).IsUnique();
        b.Property(q => q.Text).HasMaxLength(512).IsRequired();
        b.Property(q => q.Explanation).HasMaxLength(2048);
        b.Property(q => q.ExplanationSourceUrl).HasMaxLength(512);

        b.HasOne(q => q.Category)
            .WithMany(c => c.Questions)
            .HasForeignKey(q => q.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(q => q.CategoryId);
        b.HasIndex(q => q.IsActive);
    }
}
