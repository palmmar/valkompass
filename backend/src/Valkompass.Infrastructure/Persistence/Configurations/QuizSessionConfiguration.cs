using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Valkompass.Domain.Entities;

namespace Valkompass.Infrastructure.Persistence.Configurations;

public class QuizSessionConfiguration : IEntityTypeConfiguration<QuizSession>
{
    public void Configure(EntityTypeBuilder<QuizSession> b)
    {
        b.HasKey(s => s.Id);
        b.Property(s => s.ShareToken).HasMaxLength(64).IsRequired();
        b.HasIndex(s => s.ShareToken).IsUnique();
        b.Property(s => s.ResultJson).HasColumnType("jsonb").IsRequired();
    }
}
