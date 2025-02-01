using Mewdeko.Services.Impl;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Mewdeko.Database;

/// <inheritdoc />
public class MewdekoPostgresContext : MewdekoContext
{
    /// <inheritdoc />
    public MewdekoPostgresContext(DbContextOptions<MewdekoPostgresContext> options)
        : base(options)
    {
    }

    /// <inheritdoc />
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        var credentials = new BotCredentials();
        var connString = new NpgsqlConnectionStringBuilder(credentials.PsqlConnectionString)
        {
            Pooling = true,
            MinPoolSize = 20,
            MaxPoolSize = 100,
            ConnectionIdleLifetime = 300,
            ConnectionPruningInterval = 10
        }.ToString();

        optionsBuilder
            .UseNpgsql(connString, npgsqlOptions =>
            {
                npgsqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                npgsqlOptions.MaxBatchSize(1000);

                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(3),
                    errorCodesToAdd: null);
                npgsqlOptions.CommandTimeout(30);
            })
            .EnableDetailedErrors()
            .EnableSensitiveDataLogging();
    }

    // Add this to improve query performance
    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {

        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<GuildConfig>()
            .HasIndex(x => x.GuildId)
            .IsClustered();

        modelBuilder.Entity<GuildConfig>()
            .HasIndex(x => new { x.GuildId, x.Prefix });

        modelBuilder.Entity<UserXpStats>()
            .HasIndex(x => new { x.UserId, x.GuildId, x.Xp });
    }
}