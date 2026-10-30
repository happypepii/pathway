using Microsoft.EntityFrameworkCore;
using Pathway.Api.Domain;
using Pathway.Api.Infrastructure.Persistence;
using Pathway.Api.Infrastructure.Sessions;

namespace Pathway.Api.Application.Sessions;

public sealed class AnonymousSessionService(PathwayDbContext database, AnonymousSessionPolicy policy)
{
    // The caller generates the token and retains it only for the future HttpOnly cookie.
    public async Task<AnonymousSession> CreateAsync(string token, CancellationToken cancellationToken = default)
    {
        if (!SessionToken.IsWellFormed(token))
            throw new ArgumentException("Expected a generated session token.", nameof(token));

        var session = policy.Create(SessionToken.Hash(token));
        database.AnonymousSessions.Add(session);
        await database.SaveChangesAsync(cancellationToken);
        return session;
    }

    public async Task<AnonymousSession?> ResolveAsync(string? token, CancellationToken cancellationToken = default)
    {
        if (!SessionToken.IsWellFormed(token))
            return null;

        var hash = SessionToken.Hash(token!);
        var session = await database.AnonymousSessions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.TokenHash == hash, cancellationToken);

        return session is not null && !policy.IsExpired(session) ? session : null;
    }
}
