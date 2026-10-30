namespace Pathway.Api.Application;

public sealed class UseCaseException(int status, string message) : Exception(message)
{
    public int Status { get; } = status;
}
