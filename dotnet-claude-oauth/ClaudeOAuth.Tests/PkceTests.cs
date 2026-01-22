using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace ClaudeOAuth.Tests;

public class PkceTests
{
    [Fact]
    public void GenerateCodeVerifier_ReturnsBase64UrlEncodedString()
    {
        var verifier = Pkce.GenerateCodeVerifier();

        Assert.NotNull(verifier);
        Assert.NotEmpty(verifier);
        Assert.DoesNotContain("+", verifier);
        Assert.DoesNotContain("/", verifier);
        Assert.DoesNotContain("=", verifier);
    }

    [Fact]
    public void GenerateCodeVerifier_ReturnsUniqueValues()
    {
        var verifier1 = Pkce.GenerateCodeVerifier();
        var verifier2 = Pkce.GenerateCodeVerifier();

        Assert.NotEqual(verifier1, verifier2);
    }

    [Fact]
    public void GenerateCodeVerifier_HasExpectedLength()
    {
        var verifier = Pkce.GenerateCodeVerifier();
        Assert.True(verifier.Length >= 43 && verifier.Length <= 128);
    }

    [Fact]
    public void GenerateCodeChallenge_ReturnsSha256Hash()
    {
        var verifier = "test_verifier_string";
        var challenge = Pkce.GenerateCodeChallenge(verifier);

        var expectedBytes = SHA256.HashData(Encoding.UTF8.GetBytes(verifier));
        var expected = Convert.ToBase64String(expectedBytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        Assert.Equal(expected, challenge);
    }

    [Fact]
    public void GenerateCodeChallenge_ReturnsBase64UrlEncodedString()
    {
        var verifier = "any_test_verifier";
        var challenge = Pkce.GenerateCodeChallenge(verifier);

        Assert.DoesNotContain("+", challenge);
        Assert.DoesNotContain("/", challenge);
        Assert.DoesNotContain("=", challenge);
    }

    [Fact]
    public void GenerateCodeChallenge_ThrowsOnNull()
    {
        Assert.Throws<ArgumentNullException>(() => Pkce.GenerateCodeChallenge(null!));
    }

    [Fact]
    public void GenerateCodeChallenge_IsDeterministic()
    {
        var verifier = "same_verifier";
        var challenge1 = Pkce.GenerateCodeChallenge(verifier);
        var challenge2 = Pkce.GenerateCodeChallenge(verifier);

        Assert.Equal(challenge1, challenge2);
    }

    [Fact]
    public void GeneratePkce_ReturnsValidPair()
    {
        var pair = Pkce.GeneratePkce();

        Assert.NotNull(pair);
        Assert.NotEmpty(pair.Verifier);
        Assert.NotEmpty(pair.Challenge);
    }

    [Fact]
    public void GeneratePkce_ChallengeMatchesVerifier()
    {
        var pair = Pkce.GeneratePkce();
        var expectedChallenge = Pkce.GenerateCodeChallenge(pair.Verifier);

        Assert.Equal(expectedChallenge, pair.Challenge);
    }

    [Fact]
    public void GeneratePkce_ReturnsUniquePairs()
    {
        var pair1 = Pkce.GeneratePkce();
        var pair2 = Pkce.GeneratePkce();

        Assert.NotEqual(pair1.Verifier, pair2.Verifier);
        Assert.NotEqual(pair1.Challenge, pair2.Challenge);
    }
}
