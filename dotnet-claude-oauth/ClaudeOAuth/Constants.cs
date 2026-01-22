namespace ClaudeOAuth;

/// <summary>
/// Claude OAuth constants - public values used by Claude Code and compatible third-party apps.
/// </summary>
public static class Constants
{
    /// <summary>
    /// OAuth client ID (same as Claude Code / opencode).
    /// </summary>
    public const string ClaudeOAuthClientId = "9d1c250a-e61b-44d9-88ed-5944d1962f5e";

    /// <summary>
    /// OAuth authorization URL.
    /// </summary>
    public const string ClaudeOAuthAuthorizeUrl = "https://claude.ai/oauth/authorize";

    /// <summary>
    /// OAuth token exchange URL.
    /// </summary>
    public const string ClaudeOAuthTokenUrl = "https://console.anthropic.com/v1/oauth/token";

    /// <summary>
    /// OAuth redirect URI.
    /// </summary>
    public const string ClaudeOAuthRedirectUri = "https://console.anthropic.com/oauth/code/callback";

    /// <summary>
    /// OAuth scopes.
    /// </summary>
    public const string ClaudeOAuthScopes = "org:create_api_key user:profile user:inference";

    /// <summary>
    /// Anthropic API base URL.
    /// </summary>
    public const string AnthropicApiUrl = "https://api.anthropic.com/v1";

    /// <summary>
    /// Default callback port for local server.
    /// </summary>
    public const int DefaultCallbackPort = 54545;
}
