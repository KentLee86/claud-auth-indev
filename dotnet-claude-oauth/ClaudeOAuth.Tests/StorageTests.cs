using Xunit;

namespace ClaudeOAuth.Tests;

public class StorageTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _testCredentialsFile;

    public StorageTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"claude-oauth-test-{Guid.NewGuid()}");
        _testCredentialsFile = Path.Combine(_testDir, "credentials.json");
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, true);
        }
    }

    [Fact]
    public void GetConfigDir_ReturnsExpectedPath()
    {
        var configDir = CredentialStorage.GetConfigDir();
        var expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".claude-oauth"
        );

        Assert.Equal(expected, configDir);
    }

    [Fact]
    public async Task SaveAndLoadCredentials_RoundTrip()
    {
        var storage = new TestCredentialStorage(_testCredentialsFile);
        var credentials = new ClaudeOAuthCredentials
        {
            AccessToken = "test_access_token",
            RefreshToken = "test_refresh_token",
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeMilliseconds(),
            ConnectedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        await storage.SaveCredentialsAsync(credentials);
        var loaded = await storage.LoadCredentialsAsync();

        Assert.NotNull(loaded);
        Assert.Equal(credentials.AccessToken, loaded.AccessToken);
        Assert.Equal(credentials.RefreshToken, loaded.RefreshToken);
        Assert.Equal(credentials.ExpiresAt, loaded.ExpiresAt);
        Assert.Equal(credentials.ConnectedAt, loaded.ConnectedAt);
    }

    [Fact]
    public async Task LoadCredentials_ReturnsNullWhenFileNotExists()
    {
        var storage = new TestCredentialStorage(Path.Combine(_testDir, "nonexistent.json"));
        var loaded = await storage.LoadCredentialsAsync();

        Assert.Null(loaded);
    }

    [Fact]
    public async Task ClearCredentials_DeletesFile()
    {
        var storage = new TestCredentialStorage(_testCredentialsFile);
        var credentials = new ClaudeOAuthCredentials
        {
            AccessToken = "test",
            RefreshToken = "test",
            ExpiresAt = 0,
            ConnectedAt = 0
        };

        await storage.SaveCredentialsAsync(credentials);
        Assert.True(File.Exists(_testCredentialsFile));

        await storage.ClearCredentialsAsync();
        Assert.False(File.Exists(_testCredentialsFile));
    }

    [Fact]
    public async Task ClearCredentials_DoesNotThrowWhenFileNotExists()
    {
        var storage = new TestCredentialStorage(Path.Combine(_testDir, "nonexistent.json"));
        await storage.ClearCredentialsAsync();
    }

    [Fact]
    public async Task HasValidCredentials_ReturnsTrueForValidToken()
    {
        var storage = new TestCredentialStorage(_testCredentialsFile);
        var credentials = new ClaudeOAuthCredentials
        {
            AccessToken = "test",
            RefreshToken = "test",
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeMilliseconds(),
            ConnectedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        await storage.SaveCredentialsAsync(credentials);
        var isValid = await storage.HasValidCredentialsAsync();

        Assert.True(isValid);
    }

    [Fact]
    public async Task HasValidCredentials_ReturnsFalseForExpiredToken()
    {
        var storage = new TestCredentialStorage(_testCredentialsFile);
        var credentials = new ClaudeOAuthCredentials
        {
            AccessToken = "test",
            RefreshToken = "test",
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeMilliseconds(),
            ConnectedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        await storage.SaveCredentialsAsync(credentials);
        var isValid = await storage.HasValidCredentialsAsync();

        Assert.False(isValid);
    }

    [Fact]
    public async Task HasValidCredentials_ReturnsFalseForTokenExpiringSoon()
    {
        var storage = new TestCredentialStorage(_testCredentialsFile);
        var credentials = new ClaudeOAuthCredentials
        {
            AccessToken = "test",
            RefreshToken = "test",
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(3).ToUnixTimeMilliseconds(),
            ConnectedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        await storage.SaveCredentialsAsync(credentials);
        var isValid = await storage.HasValidCredentialsAsync();

        Assert.False(isValid);
    }

    [Fact]
    public async Task HasValidCredentials_ReturnsFalseWhenNoCredentials()
    {
        var storage = new TestCredentialStorage(Path.Combine(_testDir, "nonexistent.json"));
        var isValid = await storage.HasValidCredentialsAsync();

        Assert.False(isValid);
    }
}

internal class TestCredentialStorage : ICredentialStorage
{
    private readonly string _credentialsFile;

    public TestCredentialStorage(string credentialsFile)
    {
        _credentialsFile = credentialsFile;
    }

    public async Task SaveCredentialsAsync(ClaudeOAuthCredentials credentials)
    {
        var dir = Path.GetDirectoryName(_credentialsFile);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var json = System.Text.Json.JsonSerializer.Serialize(credentials, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        });
        await File.WriteAllTextAsync(_credentialsFile, json);
    }

    public async Task<ClaudeOAuthCredentials?> LoadCredentialsAsync()
    {
        if (!File.Exists(_credentialsFile))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(_credentialsFile);
        return System.Text.Json.JsonSerializer.Deserialize<ClaudeOAuthCredentials>(json, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        });
    }

    public Task ClearCredentialsAsync()
    {
        if (File.Exists(_credentialsFile))
        {
            File.Delete(_credentialsFile);
        }
        return Task.CompletedTask;
    }

    public async Task<bool> HasValidCredentialsAsync()
    {
        var credentials = await LoadCredentialsAsync();
        if (credentials == null) return false;

        var bufferMs = 5 * 60 * 1000;
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return credentials.ExpiresAt > now + bufferMs;
    }
}
