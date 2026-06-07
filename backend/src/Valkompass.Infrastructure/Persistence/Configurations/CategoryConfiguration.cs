using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Valkompass.Domain.Entities;

namespace Valkompass.Infrastructure.Persistence.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> b)
    {
        b.HasKey(c => c.Id);
        b.Property(c => c.Slug).HasMaxLength(64).IsRequired();
        b.HasIndex(c => c.Slug).IsUnique();
        b.Property(c => c.Name).HasMaxLength(128).IsRequired();
        b.Property(c => c.Description).HasMaxLength(1024);
        b.Property(c => c.Icon).HasMaxLength(64);
    }
}
