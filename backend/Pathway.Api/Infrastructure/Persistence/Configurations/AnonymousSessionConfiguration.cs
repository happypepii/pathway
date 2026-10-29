using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pathway.Api.Domain;

namespace Pathway.Api.Infrastructure.Persistence.Configurations;

public sealed class AnonymousSessionConfiguration : IEntityTypeConfiguration<AnonymousSession>
{
    public void Configure(EntityTypeBuilder<AnonymousSession> entity)
    {
        entity.ToTable("anonymous_sessions", table => table.HasCheckConstraint(
            "ck_anonymous_sessions_token_hash", "token_hash ~ '^[0-9a-fA-F]{64}$'"));
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        entity.Property(x => x.TokenHash).HasColumnName("token_hash").HasMaxLength(64).IsRequired();
        entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
        entity.Property(x => x.LastSeenAt).HasColumnName("last_seen_at").HasColumnType("timestamp with time zone");
        entity.Property(x => x.ExpiresAt).HasColumnName("expires_at").HasColumnType("timestamp with time zone");
        entity.HasIndex(x => x.TokenHash).IsUnique().HasDatabaseName("ix_anonymous_sessions_token_hash");
        entity.HasIndex(x => x.ExpiresAt).HasDatabaseName("ix_anonymous_sessions_expires_at");
    }
}
