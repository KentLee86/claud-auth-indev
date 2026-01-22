using System.Net.Http.Json;
using System.Text.Json;
using System.Web;

namespace ClaudeOAuth;

public class OAuthHandler
{
    private readonly ICredentialStorage _storage;
    private readonly HttpClient _httpClient;
    private PkcePair? _pendingPkce;

    public OAuthHandler(ICredentialStorage? storage = null, HttpClient? httpClient = null)
    {
        _storage = storage ?? new CredentialStorage();
        _httpClient = httpClient ?? new HttpClient();
    }

    public OAuthFlowResult StartOAuthFlow()
    {
        var pkce = Pkce.GeneratePkce();
        _pendingPkce = pkce;

        var queryParams = HttpUtility.ParseQueryString(string.Empty);
        queryParams["code"] = "true";
        queryParams["client_id"] = Constants.ClaudeOAuthClientId;
        queryParams["response_type"] = "code";
        queryParams["redirect_uri"] = Constants.ClaudeOAuthRedirectUri;
        queryParams["scope"] = Constants.ClaudeOAuthScopes;
        queryParams["code_challenge"] = pkce.Challenge;
        queryParams["code_challenge_method"] = "S256";
        queryParams["state"] = pkce.Verifier;

        var authUrl = $"{Constants.ClaudeOAuthAuthorizeUrl}?{queryParams}";

        return new OAuthFlowResult(authUrl, pkce.Verifier);
    }

    public async Task<ClaudeOAuthCredentials> ExchangeCodeForTokensAsync(
        string authorizationCode,
        string? codeVerifier = null)
    {
        var verifier = codeVerifier ?? _pendingPkce?.Verifier
            ?? throw new InvalidOperationException("No code verifier found. Start OAuth flow first.");

        var parts = authorizationCode.Trim().Split('#');
        var code = parts[0];
        var state = parts.Length > 1 ? parts[1] : null;

        var request = new TokenExchangeRequest
        {
            Code = code,
            State = state,
            GrantType = "authorization_code",
            ClientId = Constants.ClaudeOAuthClientId,
            RedirectUri = Constants.ClaudeOAuthRedirectUri,
            CodeVerifier = verifier
        };

        var response = await _httpClient.PostAsJsonAsync(Constants.ClaudeOAuthTokenUrl, request);

        if (!response.IsSuccessStatusCode)
        {
            var errorText = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Token exchange failed: {errorText}");
        }

        var data = await response.Content.ReadFromJsonAsync<TokenResponse>()
            ?? throw new JsonException("Failed to parse token response");

        _pendingPkce = null;

        var credentials = new ClaudeOAuthCredentials
        {
            AccessToken = data.AccessToken,
            RefreshToken = data.RefreshToken,
            ExpiresAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + (data.ExpiresIn * 1000L),
            ConnectedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        await _storage.SaveCredentialsAsync(credentials);
        return credentials;
    }

    public async Task<ClaudeOAuthCredentials> RefreshAccessTokenAsync(ClaudeOAuthCredentials credentials)
    {
        var request = new RefreshTokenRequest
        {
            GrantType = "refresh_token",
            RefreshToken = credentials.RefreshToken,
            ClientId = Constants.ClaudeOAuthClientId
        };

        var response = await _httpClient.PostAsJsonAsync(Constants.ClaudeOAuthTokenUrl, request);

        if (!response.IsSuccessStatusCode)
        {
            var errorText = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Token refresh failed: {errorText}");
        }

        var data = await response.Content.ReadFromJsonAsync<TokenResponse>()
            ?? throw new JsonException("Failed to parse token response");

        var newCredentials = new ClaudeOAuthCredentials
        {
            AccessToken = data.AccessToken,
            RefreshToken = data.RefreshToken ?? credentials.RefreshToken,
            ExpiresAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + (data.ExpiresIn * 1000L),
            ConnectedAt = credentials.ConnectedAt
        };

        await _storage.SaveCredentialsAsync(newCredentials);
        return newCredentials;
    }

    public async Task<string> GetValidAccessTokenAsync()
    {
        var credentials = await _storage.LoadCredentialsAsync()
            ?? throw new InvalidOperationException("Not authenticated. Run login first.");

        const long bufferMs = 5 * 60 * 1000;
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        if (credentials.ExpiresAt <= now + bufferMs)
        {
            credentials = await RefreshAccessTokenAsync(credentials);
        }

        return credentials.AccessToken;
    }
}
