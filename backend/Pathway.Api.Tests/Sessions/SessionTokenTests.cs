using Pathway.Api.Infrastructure.Sessions;

namespace Pathway.Api.Tests.Sessions;

public sealed class SessionTokenTests
{
    [Fact]
    public void Generated_tokens_are_32_bytes_encoded_as_unpadded_base64url()
    {
        var first = SessionToken.Generate();
        var second = SessionToken.Generate();

        Assert.Matches("^[A-Za-z0-9_-]{43}$", first);
        Assert.True(SessionToken.IsWellFormed(first));
        Assert.Equal(32, Convert.FromBase64String(first.Replace('-', '+').Replace('_', '/') + "=").Length);
        Assert.NotEqual(first, second);
        Assert.Matches("^[0-9A-F]{64}$", SessionToken.Hash(first));
    }

    [Fact]
    public void Hash_matches_known_sha256_of_utf8_token()
    {
        Assert.Equal("BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD",
            SessionToken.Hash("abc"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Missing_token_cannot_be_hashed(string? token)
    {
        Assert.ThrowsAny<ArgumentException>(() => SessionToken.Hash(token!));
    }
}
