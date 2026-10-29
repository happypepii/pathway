using Microsoft.EntityFrameworkCore;
using Pathway.Api.Domain;

namespace Pathway.Api.Infrastructure.Persistence;

public sealed class PathwayDbContext(DbContextOptions<PathwayDbContext> options) : DbContext(options)
{
    public DbSet<AnonymousSession> AnonymousSessions => Set<AnonymousSession>();
    public DbSet<Goal> Goals => Set<Goal>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(PathwayDbContext).Assembly);
}
