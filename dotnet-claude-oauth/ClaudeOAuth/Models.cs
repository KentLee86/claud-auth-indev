using System.Text.Json.Serialization;

namespace ClaudeOAuth;

/// <summary>
/// OAuth credentials stored locally.
/// </summary>
public record ClaudeOAuthCredentials
{
    /// <summary>
    /// The access token for API requests.
    /// </summary>
    public required string AccessToken { get; init; }

    /// <summary>
    /// The refresh token for obtaining new access tokens.
    /// </summary>
    public required string RefreshToken { get; init; }

    /// <summary>
    /// Unix timestamp (milliseconds) when the access token expires.
    /// </summary>
    public required long ExpiresAt { get; init; }

    /// <summary>
    /// Unix timestamp (milliseconds) when the connection was established.
    /// </summary>
    public required long ConnectedAt { get; init; }
}

/// <summary>
/// Result of starting an OAuth flow.
/// </summary>
/// <param name="AuthUrl">The URL to redirect the user to for authorization</param>
/// <param name="CodeVerifier">The PKCE code verifier to use when exchanging the code</param>
public record OAuthFlowResult(string AuthUrl, string CodeVerifier);

/// <summary>
/// Chat message.
/// </summary>
public record Message
{
    /// <summary>
    /// The role of the message sender.
    /// </summary>
    [JsonPropertyName("role")]
    public required string Role { get; init; }

    /// <summary>
    /// The content of the message.
    /// </summary>
    [JsonPropertyName("content")]
    public required string Content { get; init; }

    /// <summary>
    /// Create a user message.
    /// </summary>
    public static Message User(string content) => new() { Role = "user", Content = content };

    /// <summary>
    /// Create an assistant message.
    /// </summary>
    public static Message Assistant(string content) => new() { Role = "assistant", Content = content };
}

/// <summary>
/// Chat options for API requests.
/// </summary>
public record ChatOptions
{
    /// <summary>
    /// The model to use (default: claude-opus-4-5).
    /// </summary>
    public string? Model { get; init; }

    /// <summary>
    /// Maximum tokens to generate (default: 4096).
    /// </summary>
    public int? MaxTokens { get; init; }

    /// <summary>
    /// System prompt to use.
    /// </summary>
    public string? System { get; init; }
}

/// <summary>
/// Response from chat API.
/// </summary>
public record ChatResponse
{
    /// <summary>
    /// The unique ID of this response.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// The generated content.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// The model that generated this response.
    /// </summary>
    public required string Model { get; init; }

    /// <summary>
    /// The reason the generation stopped.
    /// </summary>
    public required string StopReason { get; init; }

    /// <summary>
    /// Token usage information.
    /// </summary>
    public required ChatUsage Usage { get; init; }
}

/// <summary>
/// Token usage information.
/// </summary>
public record ChatUsage
{
    /// <summary>
    /// Number of input tokens.
    /// </summary>
    public required int InputTokens { get; init; }

    /// <summary>
    /// Number of output tokens.
    /// </summary>
    public required int OutputTokens { get; init; }
}

// Internal DTOs for JSON serialization
internal record TokenResponse
{
    [JsonPropertyName("access_token")]
    public required string AccessToken { get; init; }

    [JsonPropertyName("refresh_token")]
    public required string RefreshToken { get; init; }

    [JsonPropertyName("expires_in")]
    public required int ExpiresIn { get; init; }

    [JsonPropertyName("token_type")]
    public required string TokenType { get; init; }
}

internal record AnthropicMessageResponse
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("role")]
    public required string Role { get; init; }

    [JsonPropertyName("content")]
    public required List<ContentBlock> Content { get; init; }

    [JsonPropertyName("model")]
    public required string Model { get; init; }

    [JsonPropertyName("stop_reason")]
    public required string StopReason { get; init; }

    [JsonPropertyName("usage")]
    public required UsageInfo Usage { get; init; }
}

internal record ContentBlock
{
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("text")]
    public string? Text { get; init; }
}

internal record UsageInfo
{
    [JsonPropertyName("input_tokens")]
    public required int InputTokens { get; init; }

    [JsonPropertyName("output_tokens")]
    public required int OutputTokens { get; init; }
}

internal record TokenExchangeRequest
{
    [JsonPropertyName("code")]
    public required string Code { get; init; }

    [JsonPropertyName("state")]
    public string? State { get; init; }

    [JsonPropertyName("grant_type")]
    public required string GrantType { get; init; }

    [JsonPropertyName("client_id")]
    public required string ClientId { get; init; }

    [JsonPropertyName("redirect_uri")]
    public required string RedirectUri { get; init; }

    [JsonPropertyName("code_verifier")]
    public required string CodeVerifier { get; init; }
}

internal record RefreshTokenRequest
{
    [JsonPropertyName("grant_type")]
    public required string GrantType { get; init; }

    [JsonPropertyName("refresh_token")]
    public required string RefreshToken { get; init; }

    [JsonPropertyName("client_id")]
    public required string ClientId { get; init; }
}

internal record ChatRequest
{
    [JsonPropertyName("model")]
    public required string Model { get; init; }

    [JsonPropertyName("max_tokens")]
    public required int MaxTokens { get; init; }

    [JsonPropertyName("system")]
    public string? System { get; init; }

    [JsonPropertyName("messages")]
    public required List<Message> Messages { get; init; }

    [JsonPropertyName("stream")]
    public bool? Stream { get; init; }
}
