using Pathway.Api.Domain;

namespace Pathway.Api.Application.Planning;

public sealed record PlanningContext(string Title, string? Description, AnswerInput Answers);
public interface IPlanGenerator
{
    Task<PlanInput> GenerateAsync(PlanningContext context, CancellationToken cancellationToken);
}

public sealed class PlanningUnavailableException : Exception { }
public sealed class PlanningOutputException : Exception { }
