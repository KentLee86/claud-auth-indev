using System.Net;
using System.Text.Json;
using Moq;
using Moq.Protected;
using Xunit;

namespace ClaudeOAuth.Tests;

public class OAuthTests
{
    [Fact]
    public void StartOAuthFlow_ReturnsValidAuthUrl()
    {
        var mockStorage = new Mock<ICredentialStorage>();
        var handler = new OAuthHandler(mockStorage.Object);

        var result = handler.StartOAuthFlow();

        Assert.NotNull(result.AuthUrl);
        Assert.Contains(Constants.ClaudeOAuthAuthorizeUrl, result.AuthUrl);
        Assert.Contains($"client_id={Constants.ClaudeOAuthClientId}", result.AuthUrl);
        Assert.Contains("response_type=code", result.AuthUrl);
        Assert.Contains("code_challenge_method=S256", result.AuthUrl);
    }

    [Fact]
    public void StartOAuthFlow_ReturnsValidCodeVerifier()
    {
        var mockStorage = new Mock<ICredentialStorage>();
        var handler = new OAuthHandler(mockStorage.Object);

        var result = handler.StartOAuthFlow();

        Assert.NotEmpty(result.CodeVerifier);
        Assert.DoesNotContain("+", result.CodeVerifier);
        Assert.DoesNotContain("/", result.CodeVerifier);
        Assert.DoesNotContain("=", result.CodeVerifier);
    }

    [Fact]
    public void StartOAuthFlow_IncludesCodeChallengeInUrl()
    {
        var mockStorage = new Mock<ICredentialStorage>();
        var handler = new OAuthHandler(mockStorage.Object);

        var result = handler.StartOAuthFlow();
        var expectedChallenge = Pkce.GenerateCodeChallenge(result.CodeVerifier);

        Assert.Contains($"code_challenge={Uri.EscapeDataString(expectedChallenge)}", result.AuthUrl);
    }

    [Fact]
    public async Task ExchangeCodeForTokens_ReturnsCredentials()
    {
        var mockStorage = new Mock<ICredentialStorage>();
        mockStorage.Setup(s => s.SaveCredentialsAsync(It.IsAny<ClaudeOAuthCredentials>()))
            .Returns(Task.CompletedTask);

        var mockHandler = new Mock<HttpMessageHandler>();
        var tokenResponse = new
        {
            access_token = "test_access_token",
            refresh_token = "test_refresh_token",
            expires_in = 3600,
            token_type = "Bearer"
        };

        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(tokenResponse))
            });

        var httpClient = new HttpClient(mockHandler.Object);
        var handler = new OAuthHandler(mockStorage.Object, httpClient);

        handler.StartOAuthFlow();
        var credentials = await handler.ExchangeCodeForTokensAsync("test_code#test_state");

        Assert.Equal("test_access_token", credentials.AccessToken);
        Assert.Equal("test_refresh_token", credentials.RefreshToken);
        Assert.True(credentials.ExpiresAt > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    }

    [Fact]
    public async Task ExchangeCodeForTokens_ThrowsOnError()
    {
        var mockStorage = new Mock<ICredentialStorage>();
        var mockHandler = new Mock<HttpMessageHandler>();

        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.BadRequest,
                Content = new StringContent("Invalid code")
            });

        var httpClient = new HttpClient(mockHandler.Object);
        var handler = new OAuthHandler(mockStorage.Object, httpClient);

        handler.StartOAuthFlow();
        await Assert.ThrowsAsync<HttpRequestException>(() => handler.ExchangeCodeForTokensAsync("bad_code"));
    }

    [Fact]
    public async Task ExchangeCodeForTokens_ThrowsWithoutStartingFlow()
    {
        var mockStorage = new Mock<ICredentialStorage>();
        var handler = new OAuthHandler(mockStorage.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.ExchangeCodeForTokensAsync("code"));
    }

    [Fact]
    public async Task RefreshAccessToken_ReturnsNewCredentials()
    {
        var mockStorage = new Mock<ICredentialStorage>();
        mockStorage.Setup(s => s.SaveCredentialsAsync(It.IsAny<ClaudeOAuthCredentials>()))
            .Returns(Task.CompletedTask);

        var mockHandler = new Mock<HttpMessageHandler>();
        var tokenResponse = new
        {
            access_token = "new_access_token",
            refresh_token = "new_refresh_token",
            expires_in = 3600,
            token_type = "Bearer"
        };

        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(tokenResponse))
            });

        var httpClient = new HttpClient(mockHandler.Object);
        var handler = new OAuthHandler(mockStorage.Object, httpClient);

        var oldCredentials = new ClaudeOAuthCredentials
        {
            AccessToken = "old_token",
            RefreshToken = "old_refresh",
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeMilliseconds(),
            ConnectedAt = DateTimeOffset.UtcNow.AddDays(-1).ToUnixTimeMilliseconds()
        };

        var newCredentials = await handler.RefreshAccessTokenAsync(oldCredentials);

        Assert.Equal("new_access_token", newCredentials.AccessToken);
        Assert.Equal("new_refresh_token", newCredentials.RefreshToken);
        Assert.Equal(oldCredentials.ConnectedAt, newCredentials.ConnectedAt);
    }

    [Fact]
    public async Task GetValidAccessToken_ReturnsTokenWhenValid()
    {
        var validCredentials = new ClaudeOAuthCredentials
        {
            AccessToken = "valid_token",
            RefreshToken = "refresh",
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeMilliseconds(),
            ConnectedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        var mockStorage = new Mock<ICredentialStorage>();
        mockStorage.Setup(s => s.LoadCredentialsAsync())
            .ReturnsAsync(validCredentials);

        var handler = new OAuthHandler(mockStorage.Object);

        var token = await handler.GetValidAccessTokenAsync();

        Assert.Equal("valid_token", token);
    }

    [Fact]
    public async Task GetValidAccessToken_RefreshesExpiredToken()
    {
        var expiredCredentials = new ClaudeOAuthCredentials
        {
            AccessToken = "expired_token",
            RefreshToken = "refresh",
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(2).ToUnixTimeMilliseconds(),
            ConnectedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        var mockStorage = new Mock<ICredentialStorage>();
        mockStorage.Setup(s => s.LoadCredentialsAsync())
            .ReturnsAsync(expiredCredentials);
        mockStorage.Setup(s => s.SaveCredentialsAsync(It.IsAny<ClaudeOAuthCredentials>()))
            .Returns(Task.CompletedTask);

        var mockHandler = new Mock<HttpMessageHandler>();
        var tokenResponse = new
        {
            access_token = "new_token",
            refresh_token = "new_refresh",
            expires_in = 3600,
            token_type = "Bearer"
        };

        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(tokenResponse))
            });

        var httpClient = new HttpClient(mockHandler.Object);
        var handler = new OAuthHandler(mockStorage.Object, httpClient);

        var token = await handler.GetValidAccessTokenAsync();

        Assert.Equal("new_token", token);
    }

    [Fact]
    public async Task GetValidAccessToken_ThrowsWhenNotAuthenticated()
    {
        var mockStorage = new Mock<ICredentialStorage>();
        mockStorage.Setup(s => s.LoadCredentialsAsync())
            .ReturnsAsync((ClaudeOAuthCredentials?)null);

        var handler = new OAuthHandler(mockStorage.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.GetValidAccessTokenAsync());
    }
}
