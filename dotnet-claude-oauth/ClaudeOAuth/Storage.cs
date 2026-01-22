using System.Text.Json;

namespace ClaudeOAuth;

/// <summary>
/// Token storage utilities - stores OAuth credentials securely in user's home directory.
/// </summary>
public class CredentialStorage : ICredentialStorage
{
    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".claude-oauth"
    );

    private static readonly string CredentialsFile = Path.Combine(ConfigDir, "credentials.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Get the config directory path.
    /// </summary>
    public static string GetConfigDir() => ConfigDir;

    /// <summary>
    /// Ensure config directory exists.
    /// </summary>
    private static void EnsureConfigDir()
    {
        if (!Directory.Exists(ConfigDir))
        {
            Directory.CreateDirectory(ConfigDir);
        }
    }

    /// <summary>
    /// Save OAuth credentials to file.
    /// </summary>
    public async Task SaveCredentialsAsync(ClaudeOAuthCredentials credentials)
    {
        EnsureConfigDir();
        var json = JsonSerializer.Serialize(credentials, JsonOptions);
        await File.WriteAllTextAsync(CredentialsFile, json);

        // Set restrictive permissions on non-Windows platforms
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(CredentialsFile, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    /// <summary>
    /// Load OAuth credentials from file.
    /// </summary>
    /// <returns>Credentials or null if not found</returns>
    public async Task<ClaudeOAuthCredentials?> LoadCredentialsAsync()
    {
        try
        {
            if (!File.Exists(CredentialsFile))
            {
                return null;
            }

            var json = await File.ReadAllTextAsync(CredentialsFile);
            return JsonSerializer.Deserialize<ClaudeOAuthCredentials>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Clear stored credentials.
    /// </summary>
    public Task ClearCredentialsAsync()
    {
        try
        {
            if (File.Exists(CredentialsFile))
            {
                File.Delete(CredentialsFile);
            }
        }
        catch
        {
            // Ignore if file doesn't exist
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Check if credentials exist and are valid (not expired).
    /// </summary>
    public async Task<bool> HasValidCredentialsAsync()
    {
        var credentials = await LoadCredentialsAsync();
        if (credentials == null) return false;

        // Check if access token is expired (with 5 minute buffer)
        var bufferMs = 5 * 60 * 1000;
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return credentials.ExpiresAt > now + bufferMs;
    }
}

/// <summary>
/// Interface for credential storage operations.
/// </summary>
public interface ICredentialStorage
{
    /// <summary>
    /// Save OAuth credentials.
    /// </summary>
    Task SaveCredentialsAsync(ClaudeOAuthCredentials credentials);

    /// <summary>
    /// Load OAuth credentials.
    /// </summary>
    Task<ClaudeOAuthCredentials?> LoadCredentialsAsync();

    /// <summary>
    /// Clear stored credentials.
    /// </summary>
    Task ClearCredentialsAsync();

    /// <summary>
    /// Check if credentials exist and are valid.
    /// </summary>
    Task<bool> HasValidCredentialsAsync();
}
