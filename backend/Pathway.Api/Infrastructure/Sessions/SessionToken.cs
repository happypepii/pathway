using System.Security.Cryptography;
using System.Text;

namespace Pathway.Api.Infrastructure.Sessions;

public static class SessionToken
{
    public static bool IsWellFormed(string? token) => token is { Length: 43 }
        && token.All(c => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '_');

    // generate 32 bytes of random number and encode it using Base64url => token
    // user take token and server saves the hashed token to ensure data safety
    public static string Generate() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static string Hash(string token)
    {
        ArgumentException.ThrowIfNullOrEmpty(token);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }
}
