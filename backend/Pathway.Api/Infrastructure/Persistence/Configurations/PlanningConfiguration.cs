using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pathway.Api.Domain;

namespace Pathway.Api.Infrastructure.Persistence.Configurations;

public sealed class PlanningAnswerConfiguration : IEntityTypeConfiguration<PlanningAnswer>
{
    public void Configure(EntityTypeBuilder<PlanningAnswer> e)
    {
        e.ToTable("planning_answers", t => t.HasCheckConstraint("ck_answers_length", "char_length(answer) <= 2000"));
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        e.Property(x => x.GoalId).HasColumnName("goal_id");
        e.Property(x => x.QuestionKey).HasColumnName("question_key").HasMaxLength(100).IsRequired();
        e.Property(x => x.Question).HasColumnName("question").HasMaxLength(500).IsRequired();
        e.Property(x => x.Answer).HasColumnName("answer").HasColumnType("text");
        e.HasIndex(x => new { x.GoalId, x.QuestionKey }).IsUnique();
        e.HasOne(x => x.Goal).WithMany(x => x.PlanningAnswers).HasForeignKey(x => x.GoalId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class MilestoneConfiguration : IEntityTypeConfiguration<Milestone>
{
    public void Configure(EntityTypeBuilder<Milestone> e)
    {
        e.ToTable("milestones", t => t.HasCheckConstraint("ck_milestones_title", "char_length(title) BETWEEN 1 AND 200"));
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        e.Property(x => x.GoalId).HasColumnName("goal_id");
        e.Property(x => x.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
        e.Property(x => x.SortOrder).HasColumnName("sort_order");
        e.HasIndex(x => new { x.GoalId, x.SortOrder }).IsUnique();
        e.HasOne(x => x.Goal).WithMany(x => x.Milestones).HasForeignKey(x => x.GoalId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class PlanTaskConfiguration : IEntityTypeConfiguration<PlanTask>
{
    public void Configure(EntityTypeBuilder<PlanTask> e)
    {
        e.ToTable("tasks", t =>
        {
            t.HasCheckConstraint("ck_tasks_effort", "estimated_minutes > 0 AND estimated_minutes % 60 = 0");
            t.HasCheckConstraint("ck_tasks_dates", "scheduled_end_date >= scheduled_start_date");
            t.HasCheckConstraint("ck_tasks_title", "char_length(title) BETWEEN 1 AND 200");
        });
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        e.Property(x => x.MilestoneId).HasColumnName("milestone_id");
        e.Property(x => x.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
        e.Property(x => x.Description).HasColumnName("description").HasMaxLength(2000);
        e.Property(x => x.EstimatedMinutes).HasColumnName("estimated_minutes");
        e.Property(x => x.ScheduledStartDate).HasColumnName("scheduled_start_date").HasColumnType("date");
        e.Property(x => x.ScheduledEndDate).HasColumnName("scheduled_end_date").HasColumnType("date");
        e.Property(x => x.SortOrder).HasColumnName("sort_order");
        e.Property(x => x.CompletedAt).HasColumnName("completed_at").HasColumnType("timestamp with time zone");
        e.HasIndex(x => new { x.MilestoneId, x.SortOrder }).IsUnique();
        e.HasOne(x => x.Milestone).WithMany(x => x.Tasks).HasForeignKey(x => x.MilestoneId).OnDelete(DeleteBehavior.Cascade);
    }
}
