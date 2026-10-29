namespace Pathway.Api.Domain;

public sealed class AnonymousSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string TokenHash { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastSeenAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public ICollection<Goal> Goals { get; set; } = new List<Goal>();
}
