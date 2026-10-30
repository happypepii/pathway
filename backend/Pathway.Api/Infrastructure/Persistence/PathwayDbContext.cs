using Microsoft.EntityFrameworkCore;
using Pathway.Api.Domain;

namespace Pathway.Api.Infrastructure.Persistence;

public sealed class PathwayDbContext(DbContextOptions<PathwayDbContext> options) : DbContext(options)
{
    public DbSet<AnonymousSession> AnonymousSessions => Set<AnonymousSession>();
    public DbSet<Goal> Goals => Set<Goal>();

    public DbSet<PlanningAnswer> PlanningAnswers => Set<PlanningAnswer>();
    public DbSet<Milestone> Milestones => Set<Milestone>();
    public DbSet<PlanTask> Tasks => Set<PlanTask>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(PathwayDbContext).Assembly);
}
