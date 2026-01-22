using System.Net;
using System.Text.Json;
using Moq;
using Moq.Protected;
using Xunit;

namespace ClaudeOAuth.Tests;

public class ClientTests
{
    private static OAuthHandler CreateMockOAuthHandler(string accessToken = "test_token")
    {
        var mockStorage = new Mock<ICredentialStorage>();
        mockStorage.Setup(s => s.LoadCredentialsAsync())
            .ReturnsAsync(new ClaudeOAuthCredentials
            {
                AccessToken = accessToken,
                RefreshToken = "refresh",
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeMilliseconds(),
                ConnectedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });

        return new OAuthHandler(mockStorage.Object);
    }

    private static HttpClient CreateMockHttpClient(object responseContent, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(JsonSerializer.Serialize(responseContent))
            });

        return new HttpClient(mockHandler.Object);
    }

    [Fact]
    public async Task ChatAsync_ReturnsValidResponse()
    {
        var apiResponse = new
        {
            id = "msg_123",
            type = "message",
            role = "assistant",
            content = new[] { new { type = "text", text = "Hello, world!" } },
            model = "claude-opus-4-5",
            stop_reason = "end_turn",
            usage = new { input_tokens = 10, output_tokens = 5 }
        };

        var oauthHandler = CreateMockOAuthHandler();
        var httpClient = CreateMockHttpClient(apiResponse);
        var client = new ClaudeClient(oauthHandler, httpClient);

        var response = await client.ChatAsync([Message.User("Hi")]);

        Assert.Equal("msg_123", response.Id);
        Assert.Equal("Hello, world!", response.Content);
        Assert.Equal("claude-opus-4-5", response.Model);
        Assert.Equal("end_turn", response.StopReason);
        Assert.Equal(10, response.Usage.InputTokens);
        Assert.Equal(5, response.Usage.OutputTokens);
    }

    [Fact]
    public async Task ChatAsync_UsesCustomOptions()
    {
        var apiResponse = new
        {
            id = "msg_123",
            type = "message",
            role = "assistant",
            content = new[] { new { type = "text", text = "Response" } },
            model = "claude-sonnet-4-5",
            stop_reason = "end_turn",
            usage = new { input_tokens = 10, output_tokens = 5 }
        };

        string? capturedContent = null;
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>(async (req, _) =>
            {
                capturedContent = await req.Content!.ReadAsStringAsync();
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(JsonSerializer.Serialize(apiResponse))
                };
            });

        var oauthHandler = CreateMockOAuthHandler();
        var httpClient = new HttpClient(mockHandler.Object);
        var client = new ClaudeClient(oauthHandler, httpClient);

        var options = new ChatOptions
        {
            Model = "claude-sonnet-4-5",
            MaxTokens = 2048,
            System = "You are a helpful assistant."
        };

        await client.ChatAsync([Message.User("Hi")], options);

        Assert.NotNull(capturedContent);
        Assert.Contains("claude-sonnet-4-5", capturedContent);
        Assert.Contains("2048", capturedContent);
        Assert.Contains("You are a helpful assistant", capturedContent);
    }

    [Fact]
    public async Task ChatAsync_ThrowsOnApiError()
    {
        var oauthHandler = CreateMockOAuthHandler();
        var httpClient = CreateMockHttpClient(new { error = "Bad request" }, HttpStatusCode.BadRequest);
        var client = new ClaudeClient(oauthHandler, httpClient);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.ChatAsync([Message.User("Hi")]));
    }

    [Fact]
    public async Task ChatAsync_IncludesCorrectHeaders()
    {
        var apiResponse = new
        {
            id = "msg_123",
            type = "message",
            role = "assistant",
            content = new[] { new { type = "text", text = "Response" } },
            model = "claude-opus-4-5",
            stop_reason = "end_turn",
            usage = new { input_tokens = 10, output_tokens = 5 }
        };

        HttpRequestMessage? capturedRequest = null;
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(apiResponse))
            });

        var oauthHandler = CreateMockOAuthHandler("my_token");
        var httpClient = new HttpClient(mockHandler.Object);
        var client = new ClaudeClient(oauthHandler, httpClient);

        await client.ChatAsync([Message.User("Hi")]);

        Assert.NotNull(capturedRequest);
        Assert.Equal("Bearer my_token", capturedRequest!.Headers.Authorization?.ToString());
        Assert.Contains("anthropic-version", capturedRequest.Headers.Select(h => h.Key));
        Assert.Contains("anthropic-beta", capturedRequest.Headers.Select(h => h.Key));
    }

    [Fact]
    public async Task AskAsync_ReturnsContentString()
    {
        var apiResponse = new
        {
            id = "msg_123",
            type = "message",
            role = "assistant",
            content = new[] { new { type = "text", text = "The answer is 42." } },
            model = "claude-opus-4-5",
            stop_reason = "end_turn",
            usage = new { input_tokens = 10, output_tokens = 5 }
        };

        var oauthHandler = CreateMockOAuthHandler();
        var httpClient = CreateMockHttpClient(apiResponse);
        var client = new ClaudeClient(oauthHandler, httpClient);

        var answer = await client.AskAsync("What is the answer?");

        Assert.Equal("The answer is 42.", answer);
    }

    [Fact]
    public void Message_User_CreatesCorrectMessage()
    {
        var message = Message.User("Hello");

        Assert.Equal("user", message.Role);
        Assert.Equal("Hello", message.Content);
    }

    [Fact]
    public void Message_Assistant_CreatesCorrectMessage()
    {
        var message = Message.Assistant("Hi there");

        Assert.Equal("assistant", message.Role);
        Assert.Equal("Hi there", message.Content);
    }

    [Fact]
    public async Task ChatStreamAsync_YieldsTextChunks()
    {
        var sseResponse = """
            data: {"type":"message_start","message":{"id":"msg_123","model":"claude-opus-4-5"}}

            data: {"type":"content_block_delta","delta":{"text":"Hello"}}

            data: {"type":"content_block_delta","delta":{"text":" world"}}

            data: {"type":"content_block_delta","delta":{"text":"!"}}

            data: {"type":"message_delta","delta":{"stop_reason":"end_turn"},"usage":{"input_tokens":10,"output_tokens":3}}

            """;

        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(sseResponse)
            });

        var oauthHandler = CreateMockOAuthHandler();
        var httpClient = new HttpClient(mockHandler.Object);
        var client = new ClaudeClient(oauthHandler, httpClient);

        var chunks = new List<string>();
        await foreach (var chunk in client.ChatStreamAsync([Message.User("Hi")]))
        {
            chunks.Add(chunk);
        }

        Assert.Equal(3, chunks.Count);
        Assert.Equal("Hello", chunks[0]);
        Assert.Equal(" world", chunks[1]);
        Assert.Equal("!", chunks[2]);
    }

    [Fact]
    public async Task ChatStreamAsync_ThrowsOnApiError()
    {
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.Unauthorized,
                Content = new StringContent("Invalid token")
            });

        var oauthHandler = CreateMockOAuthHandler();
        var httpClient = new HttpClient(mockHandler.Object);
        var client = new ClaudeClient(oauthHandler, httpClient);

        await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await foreach (var _ in client.ChatStreamAsync([Message.User("Hi")]))
            {
            }
        });
    }
}
