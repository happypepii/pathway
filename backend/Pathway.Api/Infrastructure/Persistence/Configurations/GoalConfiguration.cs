using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pathway.Api.Domain;

namespace Pathway.Api.Infrastructure.Persistence.Configurations;

public sealed class GoalConfiguration : IEntityTypeConfiguration<Goal>
{
    public void Configure(EntityTypeBuilder<Goal> entity)
    {
        entity.ToTable("goals", table =>
        {
            table.HasCheckConstraint("ck_goals_title", "char_length(title) >= 1 AND title !~ '^[[:space:]]|[[:space:]]$'");
            table.HasCheckConstraint("ck_goals_description", "char_length(description) <= 2000");
            table.HasCheckConstraint("ck_goals_capacity", "available_hours_per_week BETWEEN 1 AND 168");
            table.HasCheckConstraint("ck_goals_status", "status IN ('Draft', 'Active', 'Completed', 'Archived')");
        });
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        entity.Property(x => x.SessionId).HasColumnName("session_id");
        entity.Property(x => x.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
        entity.Property(x => x.Description).HasColumnName("description").HasColumnType("text");
        entity.Property(x => x.TargetDate).HasColumnName("target_date").HasColumnType("date");
        entity.Property(x => x.AvailableHoursPerWeek).HasColumnName("available_hours_per_week");
        entity.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
        entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");
        entity.HasIndex(x => x.SessionId).HasDatabaseName("ix_goals_session_id");
        entity.HasOne(x => x.Session).WithMany(x => x.Goals).HasForeignKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
