using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace ClaudeOAuth;

public class ClaudeClient
{
    private const string DefaultModel = "claude-opus-4-5";
    private const int DefaultMaxTokens = 4096;
    private const string ClaudeCodeSystemPrefix = "You are Claude Code, Anthropic's official CLI for Claude.";
    private const string AnthropicBetaFlags = "oauth-2025-04-20,interleaved-thinking-2025-05-14";
    private const string UserAgent = "claude-cli/2.1.2 (external, cli)";

    private readonly OAuthHandler _oauthHandler;
    private readonly HttpClient _httpClient;

    public ClaudeClient(OAuthHandler? oauthHandler = null, HttpClient? httpClient = null)
    {
        _oauthHandler = oauthHandler ?? new OAuthHandler();
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task<ChatResponse> ChatAsync(IEnumerable<Message> messages, ChatOptions? options = null)
    {
        options ??= new ChatOptions();
        var accessToken = await _oauthHandler.GetValidAccessTokenAsync();

        var systemPrompt = string.IsNullOrEmpty(options.System)
            ? ClaudeCodeSystemPrefix
            : $"{ClaudeCodeSystemPrefix}\n\n{options.System}";

        var request = new ChatRequest
        {
            Model = options.Model ?? DefaultModel,
            MaxTokens = options.MaxTokens ?? DefaultMaxTokens,
            System = systemPrompt,
            Messages = messages.ToList()
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{Constants.AnthropicApiUrl}/messages?beta=true");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        httpRequest.Headers.Add("anthropic-version", "2023-06-01");
        httpRequest.Headers.Add("anthropic-beta", AnthropicBetaFlags);
        httpRequest.Headers.Add("User-Agent", UserAgent);
        httpRequest.Content = JsonContent.Create(request);

        var response = await _httpClient.SendAsync(httpRequest);

        if (!response.IsSuccessStatusCode)
        {
            var errorText = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"API request failed: {(int)response.StatusCode} - {errorText}");
        }

        var data = await response.Content.ReadFromJsonAsync<AnthropicMessageResponse>()
            ?? throw new JsonException("Failed to parse API response");

        return new ChatResponse
        {
            Id = data.Id,
            Content = data.Content.FirstOrDefault()?.Text ?? "",
            Model = data.Model,
            StopReason = data.StopReason,
            Usage = new ChatUsage
            {
                InputTokens = data.Usage.InputTokens,
                OutputTokens = data.Usage.OutputTokens
            }
        };
    }

    public async IAsyncEnumerable<string> ChatStreamAsync(
        IEnumerable<Message> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        options ??= new ChatOptions();
        var accessToken = await _oauthHandler.GetValidAccessTokenAsync();

        var systemPrompt = string.IsNullOrEmpty(options.System)
            ? ClaudeCodeSystemPrefix
            : $"{ClaudeCodeSystemPrefix}\n\n{options.System}";

        var request = new ChatRequest
        {
            Model = options.Model ?? DefaultModel,
            MaxTokens = options.MaxTokens ?? DefaultMaxTokens,
            System = systemPrompt,
            Messages = messages.ToList(),
            Stream = true
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{Constants.AnthropicApiUrl}/messages?beta=true");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        httpRequest.Headers.Add("anthropic-version", "2023-06-01");
        httpRequest.Headers.Add("anthropic-beta", AnthropicBetaFlags);
        httpRequest.Headers.Add("User-Agent", UserAgent);
        httpRequest.Content = JsonContent.Create(request);

        var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorText = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException($"API request failed: {(int)response.StatusCode} - {errorText}");
        }

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrEmpty(line)) continue;

            if (!line.StartsWith("data: ")) continue;

            var jsonStr = line[6..];
            if (jsonStr == "[DONE]") continue;

            var text = ParseStreamEvent(jsonStr);
            if (!string.IsNullOrEmpty(text))
            {
                yield return text;
            }
        }
    }

    private static string? ParseStreamEvent(string jsonStr)
    {
        JsonDocument? eventDoc = null;
        try
        {
            eventDoc = JsonDocument.Parse(jsonStr);
            var root = eventDoc.RootElement;

            if (root.TryGetProperty("type", out var typeElement))
            {
                var eventType = typeElement.GetString();

                if (eventType == "content_block_delta" &&
                    root.TryGetProperty("delta", out var delta) &&
                    delta.TryGetProperty("text", out var textElement))
                {
                    return textElement.GetString();
                }
            }
        }
        catch (JsonException)
        {
            return null;
        }
        finally
        {
            eventDoc?.Dispose();
        }

        return null;
    }

    public async Task<string> AskAsync(string prompt, ChatOptions? options = null)
    {
        var response = await ChatAsync([Message.User(prompt)], options);
        return response.Content;
    }
}
