using Pathway.Api.Domain;

namespace Pathway.Api.Application.Sessions;

public sealed class AnonymousSessionPolicy(TimeProvider timeProvider)
{
    public AnonymousSession Create(string tokenHash)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        return new AnonymousSession
        {
            TokenHash = tokenHash,
            CreatedAt = now,
            LastSeenAt = now,
            ExpiresAt = now.AddDays(30)
        };
    }

    public bool IsExpired(AnonymousSession session) =>
        timeProvider.GetUtcNow().UtcDateTime >= session.ExpiresAt;
}
