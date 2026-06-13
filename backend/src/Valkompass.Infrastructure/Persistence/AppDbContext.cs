using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Valkompass.Domain.Entities;
using Valkompass.Infrastructure.Identity;

namespace Valkompass.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Party> Parties => Set<Party>();
    public DbSet<PartyLogo> PartyLogos => Set<PartyLogo>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<PartyPosition> PartyPositions => Set<PartyPosition>();
    public DbSet<QuizSession> QuizSessions => Set<QuizSession>();
    public DbSet<Answer> Answers => Set<Answer>();

    // Valbarometer (fristående opinionsdata – ingen koppling till quiz/resultat).
    public DbSet<Pollster> Pollsters => Set<Pollster>();
    public DbSet<Poll> Polls => Set<Poll>();
    public DbSet<PollResult> PollResults => Set<PollResult>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
