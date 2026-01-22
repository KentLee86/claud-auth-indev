using System.Security.Cryptography;
using System.Text;

namespace ClaudeOAuth;

/// <summary>
/// PKCE (Proof Key for Code Exchange) utilities for secure OAuth flow without client secrets.
/// </summary>
public static class Pkce
{
    /// <summary>
    /// Generate a random code verifier for PKCE.
    /// </summary>
    /// <returns>Base64url encoded random string</returns>
    public static string GenerateCodeVerifier()
    {
        var buffer = new byte[64];
        RandomNumberGenerator.Fill(buffer);
        return Base64UrlEncode(buffer);
    }

    /// <summary>
    /// Generate a code challenge from a verifier using SHA256.
    /// </summary>
    /// <param name="verifier">The code verifier string</param>
    /// <returns>Base64url encoded SHA256 hash</returns>
    public static string GenerateCodeChallenge(string verifier)
    {
        ArgumentNullException.ThrowIfNull(verifier);
        
        var bytes = Encoding.UTF8.GetBytes(verifier);
        var hash = SHA256.HashData(bytes);
        return Base64UrlEncode(hash);
    }

    /// <summary>
    /// Generate a complete PKCE pair (verifier and challenge).
    /// </summary>
    /// <returns>A PKCEPair containing verifier and challenge</returns>
    public static PkcePair GeneratePkce()
    {
        var verifier = GenerateCodeVerifier();
        var challenge = GenerateCodeChallenge(verifier);
        return new PkcePair(verifier, challenge);
    }

    /// <summary>
    /// Encode bytes to base64url format (URL-safe base64 without padding).
    /// </summary>
    private static string Base64UrlEncode(byte[] data)
    {
        return Convert.ToBase64String(data)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}

/// <summary>
/// PKCE verifier and challenge pair.
/// </summary>
/// <param name="Verifier">The code verifier (random string)</param>
/// <param name="Challenge">The code challenge (SHA256 hash of verifier)</param>
public record PkcePair(string Verifier, string Challenge);
