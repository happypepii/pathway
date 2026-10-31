using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Pathway.Api.Domain;

namespace Pathway.Api.Tests.Persistence;

// xUnit runs tests in this class sequentially; only this fixture's generated database is reset.
public sealed class PersistenceTests(PostgresFixture database) : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private static readonly DateTime Now = new(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc);

    private static AnonymousSession Session() => new()
    {
        TokenHash = Convert.ToHexString(SHA256.HashData(Guid.NewGuid().ToByteArray())),
        CreatedAt = Now, LastSeenAt = Now, ExpiresAt = Now.AddDays(30)
    };

    private static Goal Goal(AnonymousSession session) => new()
    {
        Session = session, SessionId = session.Id, Title = "Learn a language",
        TargetDate = new DateOnly(2027, 1, 15), AvailableHoursPerWeek = 10,
        CreatedAt = Now, UpdatedAt = Now
    };

    public async Task InitializeAsync()
    {
        await using var context = database.CreateContext();
        await context.Goals.ExecuteDeleteAsync();
        await context.AnonymousSessions.ExecuteDeleteAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Guidance_migration_preserves_existing_plan_rows_and_supplies_empty_metadata()
    {
        // This fixture owns a GUID-named disposable database, never development data.
        await using var context = database.CreateContext();
        var migrator = context.Database.GetService<IMigrator>();
        await migrator.MigrateAsync("20261030153634_PlanningTables");
        try
        {
            var goal = Goal(Session());
            context.Goals.Add(goal);
            await context.SaveChangesAsync();
            var milestone = Guid.NewGuid();
            var task = Guid.NewGuid();
            await context.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO milestones (id, goal_id, title, sort_order) VALUES ({milestone}, {goal.Id}, 'Legacy stage', 0)");
            await context.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO tasks (id, milestone_id, title, description, estimated_minutes, scheduled_start_date, scheduled_end_date, sort_order) VALUES ({task}, {milestone}, 'Legacy task', 'Existing notes', 120, DATE '2026-10-31', DATE '2026-11-01', 0)");
            await migrator.MigrateAsync();
            await using var reader = database.CreateContext();
            var saved = await reader.Milestones.Include(x => x.Tasks).SingleAsync();
            Assert.Equal(milestone, saved.Id);
            Assert.Null(saved.Description);
            Assert.Null(saved.Outcome);
            Assert.Equal("[]", saved.ResourcesJson);
            Assert.Equal(task, saved.Tasks[0].Id);
            Assert.Equal("Existing notes", saved.Tasks[0].Description);
            Assert.Empty(saved.Tasks[0].KeyPoints);
            Assert.Equal(120, saved.Tasks[0].EstimatedMinutes);
        }
        finally { await migrator.MigrateAsync(); }
    }

    [Fact]
    public async Task Migration_creates_only_expected_tables_columns_and_indexes()
    {
        await using var context = database.CreateContext();
        Assert.Equal(context.Database.GetMigrations(), await context.Database.GetAppliedMigrationsAsync());
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        Assert.False(context.Database.HasPendingModelChanges());
        var tables = await context.Database.SqlQueryRaw<string>(
            "SELECT table_name AS \"Value\" FROM information_schema.tables WHERE table_schema = 'public'").ToListAsync();
        Assert.Equal(new[] { "__EFMigrationsHistory", "anonymous_sessions", "goals", "milestones", "planning_answers", "tasks" }, tables.Order().ToArray());
        var columns = await context.Database.SqlQueryRaw<string>(
            "SELECT column_name AS \"Value\" FROM information_schema.columns WHERE table_name = 'anonymous_sessions' AND table_schema = 'public'").ToListAsync();
        Assert.Equal(new[] { "created_at", "expires_at", "id", "last_seen_at", "token_hash" }, columns.Order().ToArray());
        var indexes = await context.Database.SqlQueryRaw<string>(
            "SELECT indexname AS \"Value\" FROM pg_indexes WHERE schemaname = 'public'").ToListAsync();
        Assert.Contains("ix_anonymous_sessions_token_hash", indexes);
        Assert.Contains("ix_anonymous_sessions_expires_at", indexes);
        Assert.Contains("ix_goals_session_id", indexes);
    }

    [Theory]
    [InlineData(null, 1)]
    [InlineData("Some context", 168)]
    public async Task Round_trip_preserves_dates_utc_timestamps_hash_and_draft(string? description, int hours)
    {
        var session = Session();
        var goal = Goal(session);
        goal.Description = description;
        goal.AvailableHoursPerWeek = hours;
        await using (var writer = database.CreateContext())
        {
            writer.Goals.Add(goal);
            await writer.SaveChangesAsync();
        }
        await using var reader = database.CreateContext();
        var saved = await reader.Goals.Include(x => x.Session).SingleAsync(x => x.Id == goal.Id);
        Assert.Equal(goal.TargetDate, saved.TargetDate);
        Assert.Equal(description, saved.Description);
        Assert.Equal(hours, saved.AvailableHoursPerWeek);
        Assert.Equal(GoalStatus.Draft, saved.Status);
        Assert.Equal(Now, saved.CreatedAt);
        Assert.Equal(Now, saved.UpdatedAt);
        Assert.Equal(session.TokenHash, saved.Session.TokenHash);
        Assert.Equal(Now, saved.Session.CreatedAt);
        Assert.Equal(Now, saved.Session.LastSeenAt);
        Assert.Equal(Now.AddDays(30), saved.Session.ExpiresAt);
        Assert.Equal(DateTimeKind.Utc, saved.Session.CreatedAt.Kind);
        Assert.Equal(DateTimeKind.Utc, saved.Session.LastSeenAt.Kind);
        Assert.Equal(DateTimeKind.Utc, saved.Session.ExpiresAt.Kind);
        saved.Session.LastSeenAt = Now.AddDays(1);
        await reader.SaveChangesAsync();
        await using var verify = database.CreateContext();
        Assert.Equal(Now.AddDays(30), (await verify.AnonymousSessions.SingleAsync()).ExpiresAt);
    }

    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public async Task Non_utc_lifecycle_timestamp_is_rejected(DateTimeKind kind)
    {
        await using var context = database.CreateContext();
        var session = Session();
        session.CreatedAt = DateTime.SpecifyKind(Now, kind);
        context.Add(session);
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.IsType<ArgumentException>(error.InnerException);
    }

    [Fact]
    public async Task Duplicate_hash_is_rejected()
    {
        await using var context = database.CreateContext();
        var first = Session();
        var second = Session();
        second.TokenHash = first.TokenHash;
        context.AddRange(first, second);
        await AssertDatabaseError(() => context.SaveChangesAsync(), PostgresErrorCodes.UniqueViolation);
    }

    [Theory]
    [InlineData(null, PostgresErrorCodes.NotNullViolation)]
    [InlineData("abc", PostgresErrorCodes.CheckViolation)]
    [InlineData("gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg", PostgresErrorCodes.CheckViolation)]
    public async Task Invalid_hash_is_rejected(string? hash, string code)
    {
        await using var context = database.CreateContext();
        var session = Session();
        session.TokenHash = hash!;
        context.Add(session);
        await AssertDatabaseError(() => context.SaveChangesAsync(), code);
    }

    [Theory]
    [InlineData("empty", PostgresErrorCodes.CheckViolation)]
    [InlineData("spaces", PostgresErrorCodes.CheckViolation)]
    [InlineData("untrimmed", PostgresErrorCodes.CheckViolation)]
    [InlineData("tab-title", PostgresErrorCodes.CheckViolation)]
    [InlineData("long-title", PostgresErrorCodes.StringDataRightTruncation)]
    [InlineData("null-title", PostgresErrorCodes.NotNullViolation)]
    [InlineData("long-description", PostgresErrorCodes.CheckViolation)]
    [InlineData("zero-hours", PostgresErrorCodes.CheckViolation)]
    [InlineData("excess-hours", PostgresErrorCodes.CheckViolation)]
    [InlineData("invalid-status", PostgresErrorCodes.CheckViolation)]
    public async Task Invalid_goal_is_rejected(string scenario, string code)
    {
        await using var context = database.CreateContext();
        var goal = Goal(Session());
        switch (scenario)
        {
            case "empty": goal.Title = ""; break;
            case "spaces": goal.Title = "   "; break;
            case "untrimmed": goal.Title = " Title "; break;
            case "tab-title": goal.Title = "\t"; break;
            case "long-title": goal.Title = new string('a', 201); break;
            case "null-title": goal.Title = null!; break;
            case "long-description": goal.Description = new string('a', 2001); break;
            case "zero-hours": goal.AvailableHoursPerWeek = 0; break;
            case "excess-hours": goal.AvailableHoursPerWeek = 169; break;
            case "invalid-status": goal.Status = (GoalStatus)99; break;
            default: throw new ArgumentOutOfRangeException(nameof(scenario));
        }
        context.Goals.Add(goal);
        await AssertDatabaseError(() => context.SaveChangesAsync(), code);
    }

    [Fact]
    public async Task Missing_session_is_rejected()
    {
        await using var context = database.CreateContext();
        var goal = Goal(Session());
        goal.Session = null!;
        context.Goals.Add(goal);
        await AssertDatabaseError(() => context.SaveChangesAsync(), PostgresErrorCodes.ForeignKeyViolation);
    }

    [Fact]
    public async Task Session_with_goals_cannot_be_deleted()
    {
        var goal = Goal(Session());
        await using (var writer = database.CreateContext())
        {
            writer.Goals.Add(goal);
            await writer.SaveChangesAsync();
        }
        await using var context = database.CreateContext();
        // Bypass client relationship fixup to exercise the database FK.
        var error = await Assert.ThrowsAsync<PostgresException>(() => context.AnonymousSessions.ExecuteDeleteAsync());
        Assert.Equal(PostgresErrorCodes.RestrictViolation, error.SqlState);
        Assert.Equal(1, await context.Goals.CountAsync());
        Assert.Equal(1, await context.AnonymousSessions.CountAsync());
        await context.Goals.ExecuteDeleteAsync();
        Assert.Equal(1, await context.AnonymousSessions.ExecuteDeleteAsync());
    }

    [Fact]
    public async Task Boundary_lengths_and_all_documented_statuses_are_supported()
    {
        await using var context = database.CreateContext();
        var session = Session();
        foreach (var status in Enum.GetValues<GoalStatus>())
        {
            var goal = Goal(session);
            goal.Title = new string('a', 200);
            goal.Description = new string('a', 2000);
            goal.Status = status;
            context.Goals.Add(goal);
        }
        await context.SaveChangesAsync();
        await using var reader = database.CreateContext();
        Assert.Equal(4, await reader.Goals.CountAsync());
    }

    private static async Task AssertDatabaseError(Func<Task> action, string code)
    {
        var error = await Assert.ThrowsAsync<DbUpdateException>(action);
        Assert.Equal(code, Assert.IsType<PostgresException>(error.InnerException).SqlState);
    }
}
