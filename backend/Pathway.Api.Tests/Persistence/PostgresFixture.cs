using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pathway.Api.Infrastructure.Persistence;

namespace Pathway.Api.Tests.Persistence;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly string databaseName = $"pathway_test_{Guid.NewGuid():N}";
    private string adminConnection = null!;
    private string testConnection = null!;
    private bool created;

    public PathwayDbContext CreateContext() => new(new DbContextOptionsBuilder<PathwayDbContext>()
        .UseNpgsql(testConnection).Options);

    public async Task InitializeAsync()
    {
        var connection = Environment.GetEnvironmentVariable("PATHWAY_TEST_CONNECTION")
            ?? throw new InvalidOperationException("Run bash scripts/test-persistence.sh; no development connection is used.");
        var builder = new NpgsqlConnectionStringBuilder(connection);
        if (builder.Database != "pathway_tests" || builder.Username != "pathway_tests")
            throw new InvalidOperationException("Integration tests require the dedicated pathway_tests database and role.");
        builder.Pooling = false;
        builder.IncludeErrorDetail = false;
        adminConnection = builder.ConnectionString;
        builder.Database = databaseName;
        testConnection = builder.ConnectionString;

        await using var admin = new NpgsqlConnection(adminConnection);
        await admin.OpenAsync();
        // Identifier is generated locally from a fixed prefix and GUID, never supplied by configuration.
        await using var create = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", admin);
        await create.ExecuteNonQueryAsync();
        created = true;
        try
        {
            await using var context = CreateContext();
            await context.Database.MigrateAsync();
        }
        catch
        {
            await DisposeAsync();
            throw;
        }
    }

    public async Task DisposeAsync()
    {
        if (!created) return;
        await using var admin = new NpgsqlConnection(adminConnection);
        await admin.OpenAsync();
        await using var drop = new NpgsqlCommand($"DROP DATABASE \"{databaseName}\" WITH (FORCE)", admin);
        await drop.ExecuteNonQueryAsync();
        created = false;
    }
}
