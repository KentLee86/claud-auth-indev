using ClaudeOAuth;

namespace ClaudeOAuth.Cli;

public static class Program
{
    private static readonly Dictionary<string, string> Models = new()
    {
        ["haiku"] = "claude-haiku-4-5-20251001",
        ["sonnet"] = "claude-sonnet-4-20250514",
        ["opus"] = "claude-opus-4-20250514"
    };

    private const string Help = """
        Claude OAuth Client

        Commands:
          login     Start OAuth flow to authenticate with Claude
          logout    Clear stored credentials
          status    Check authentication status
          chat      Interactive chat with Claude
          ask       Send a single message (usage: ask "your question")
          help      Show this help message

        Examples:
          dotnet run -- login
          dotnet run -- ask "What is 2+2?"
          dotnet run -- chat
        """;

    public static async Task Main(string[] args)
    {
        var command = args.Length > 0 ? args[0] : null;

        switch (command)
        {
            case "login":
                await LoginAsync();
                break;
            case "logout":
                await LogoutAsync();
                break;
            case "status":
                await StatusAsync();
                break;
            case "chat":
                await InteractiveChatAsync();
                break;
            case "ask":
                var question = string.Join(" ", args.Skip(1));
                if (string.IsNullOrWhiteSpace(question))
                {
                    Console.WriteLine("Usage: ask \"your question\"");
                    Environment.Exit(1);
                }
                await SingleAskAsync(question);
                break;
            case "help":
            case "--help":
            case "-h":
            case null:
                Console.WriteLine(Help);
                break;
            default:
                Console.WriteLine($"Unknown command: {command}");
                Console.WriteLine(Help);
                Environment.Exit(1);
                break;
        }
    }

    private static void OpenBrowser(string url)
    {
        try
        {
            if (OperatingSystem.IsMacOS())
            {
                System.Diagnostics.Process.Start("open", url);
            }
            else if (OperatingSystem.IsWindows())
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            else
            {
                System.Diagnostics.Process.Start("xdg-open", url);
            }
        }
        catch
        {
        }
    }

    private static async Task LoginAsync()
    {
        Console.WriteLine("Starting OAuth flow...\n");

        var oauthHandler = new OAuthHandler();
        var result = oauthHandler.StartOAuthFlow();

        Console.WriteLine("Opening browser...\n");
        OpenBrowser(result.AuthUrl);

        Console.WriteLine("If browser did not open, visit this URL:\n");
        Console.WriteLine(result.AuthUrl);
        Console.WriteLine("\nAfter authorizing, you will receive a code in format: CODE#STATE");
        Console.WriteLine("Copy the entire code including the # part.\n");

        Console.Write("Paste the authorization code here: ");
        var authCode = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(authCode))
        {
            Console.WriteLine("\nNo code provided.");
            Environment.Exit(1);
        }

        try
        {
            var credentials = await oauthHandler.ExchangeCodeForTokensAsync(authCode, result.CodeVerifier);
            Console.WriteLine("\nSuccess! You are now authenticated.");
            Console.WriteLine($"Credentials saved to: {CredentialStorage.GetConfigDir()}");
            Console.WriteLine($"Token expires: {DateTimeOffset.FromUnixTimeMilliseconds(credentials.ExpiresAt).LocalDateTime}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nAuthentication failed: {ex.Message}");
            Environment.Exit(1);
        }
    }

    private static async Task LogoutAsync()
    {
        var storage = new CredentialStorage();
        await storage.ClearCredentialsAsync();
        Console.WriteLine("Credentials cleared.");
    }

    private static async Task StatusAsync()
    {
        var storage = new CredentialStorage();
        var hasValid = await storage.HasValidCredentialsAsync();

        if (!hasValid)
        {
            Console.WriteLine("Not authenticated. Run \"login\" to authenticate.");
            return;
        }

        var credentials = await storage.LoadCredentialsAsync();
        if (credentials != null)
        {
            Console.WriteLine("Authenticated");
            Console.WriteLine($"Token expires: {DateTimeOffset.FromUnixTimeMilliseconds(credentials.ExpiresAt).LocalDateTime}");
            Console.WriteLine($"Connected since: {DateTimeOffset.FromUnixTimeMilliseconds(credentials.ConnectedAt).LocalDateTime}");
            Console.WriteLine($"Config directory: {CredentialStorage.GetConfigDir()}");
        }
    }

    private static async Task InteractiveChatAsync()
    {
        var storage = new CredentialStorage();
        var hasValid = await storage.HasValidCredentialsAsync();

        if (!hasValid)
        {
            Console.WriteLine("Not authenticated. Run \"login\" first.");
            Environment.Exit(1);
        }

        var client = new ClaudeClient();
        var messages = new List<Message>();
        var currentModel = "sonnet";
        var pendingFiles = new List<MessageContentBlock>();

        Console.WriteLine("\u001b[1mClaude Chat\u001b[0m");
        Console.WriteLine("\u001b[90mType /help for commands, Ctrl+C to exit\u001b[0m");
        Console.WriteLine();

        while (true)
        {
            var fileIndicator = pendingFiles.Count > 0 ? $"\u001b[35m📎{pendingFiles.Count}\u001b[0m " : "";
            var modelIndicator = $"\u001b[90m[{currentModel}]\u001b[0m";
            Console.Write($"{fileIndicator}{modelIndicator} You: ");

            var input = Console.ReadLine();
            if (input == null)
            {
                Console.WriteLine("\nGoodbye!");
                break;
            }

            var trimmed = input.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                continue;
            }

            var lower = trimmed.ToLower();
            if (lower == "/exit" || lower == "exit")
            {
                Console.WriteLine("Goodbye!");
                break;
            }

            if (lower == "/haiku")
            {
                currentModel = "haiku";
                Console.WriteLine("\u001b[32m✓ Switched to haiku\u001b[0m\n");
                continue;
            }

            if (lower == "/sonnet")
            {
                currentModel = "sonnet";
                Console.WriteLine("\u001b[32m✓ Switched to sonnet\u001b[0m\n");
                continue;
            }

            if (lower == "/opus")
            {
                currentModel = "opus";
                Console.WriteLine("\u001b[32m✓ Switched to opus\u001b[0m\n");
                continue;
            }

            if (lower == "/clear")
            {
                pendingFiles.Clear();
                Console.WriteLine("\u001b[32m✓ Cleared attached files\u001b[0m\n");
                continue;
            }

            if (lower == "/help")
            {
                PrintChatHelp();
                continue;
            }

            if (lower.StartsWith("/file "))
            {
                var filePath = trimmed[6..].Trim();
                var block = await LoadFileAsync(filePath);
                if (block != null)
                {
                    pendingFiles.Add(block);
                    var fileName = Path.GetFileName(filePath);
                    var fileType = block is ImageContentBlock ? "🖼️" : "📄";
                    Console.WriteLine($"\u001b[32m✓ Attached: {fileType} {fileName}\u001b[0m\n");
                }
                else
                {
                    Console.WriteLine($"\u001b[31m✗ File not found or unsupported: {filePath}\u001b[0m\n");
                }
                continue;
            }

            var content = new List<MessageContentBlock>(pendingFiles)
            {
                new TextContentBlock { Text = trimmed }
            };
            pendingFiles.Clear();

            messages.Add(Message.User(content.ToArray()));

            try
            {
                Console.Write("\u001b[36mClaude:\u001b[0m ");

                var fullResponse = new System.Text.StringBuilder();
                var startTime = DateTime.UtcNow;
                var inputTokens = 0;
                var outputTokens = 0;

                await foreach (var chunk in client.ChatStreamAsync(messages, new ChatOptions { Model = Models[currentModel] }))
                {
                    Console.Write(chunk);
                    fullResponse.Append(chunk);
                }

                var elapsed = (DateTime.UtcNow - startTime).TotalSeconds;

                var response = await client.ChatAsync(messages, new ChatOptions { Model = Models[currentModel] });
                inputTokens = response.Usage.InputTokens;
                outputTokens = response.Usage.OutputTokens;
                var tokensPerSec = outputTokens / elapsed;

                Console.WriteLine("\n");
                Console.WriteLine($"\u001b[90m[{inputTokens} in / {outputTokens} out | {tokensPerSec:F1} tok/s | {elapsed:F1}s]\u001b[0m");
                Console.WriteLine();

                messages.Add(Message.Assistant(fullResponse.ToString()));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n\u001b[31mError:\u001b[0m {ex.Message}\n");
            }
        }
    }

    private static void PrintChatHelp()
    {
        Console.WriteLine();
        Console.WriteLine("\u001b[90m┌──────────────────────────────────────────────┐\u001b[0m");
        Console.WriteLine("\u001b[90m│\u001b[0m  \u001b[1mCommands\u001b[0m                                     \u001b[90m│\u001b[0m");
        Console.WriteLine("\u001b[90m├──────────────────────────────────────────────┤\u001b[0m");
        Console.WriteLine("\u001b[90m│\u001b[0m  \u001b[33m/haiku\u001b[0m          Switch to Haiku             \u001b[90m│\u001b[0m");
        Console.WriteLine("\u001b[90m│\u001b[0m  \u001b[33m/sonnet\u001b[0m         Switch to Sonnet            \u001b[90m│\u001b[0m");
        Console.WriteLine("\u001b[90m│\u001b[0m  \u001b[33m/opus\u001b[0m           Switch to Opus              \u001b[90m│\u001b[0m");
        Console.WriteLine("\u001b[90m│\u001b[0m  \u001b[33m/file <path>\u001b[0m    Attach file (image/text)   \u001b[90m│\u001b[0m");
        Console.WriteLine("\u001b[90m│\u001b[0m  \u001b[33m/clear\u001b[0m          Clear attached files        \u001b[90m│\u001b[0m");
        Console.WriteLine("\u001b[90m│\u001b[0m  \u001b[33m/help\u001b[0m           Show this help              \u001b[90m│\u001b[0m");
        Console.WriteLine("\u001b[90m│\u001b[0m  \u001b[33m/exit\u001b[0m           Exit                        \u001b[90m│\u001b[0m");
        Console.WriteLine("\u001b[90m└──────────────────────────────────────────────┘\u001b[0m");
        Console.WriteLine();
    }

    private static async Task<MessageContentBlock?> LoadFileAsync(string filePath)
    {
        try
        {
            var fullPath = Path.GetFullPath(filePath);
            if (!File.Exists(fullPath))
            {
                return null;
            }

            var extension = Path.GetExtension(fullPath).ToLowerInvariant();
            var imageExtensions = new[] { ".png", ".jpg", ".jpeg", ".gif", ".webp" };

            if (imageExtensions.Contains(extension))
            {
                var imageData = await File.ReadAllBytesAsync(fullPath);
                var mediaType = extension switch
                {
                    ".png" => "image/png",
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".gif" => "image/gif",
                    ".webp" => "image/webp",
                    _ => "image/png"
                };

                return new ImageContentBlock
                {
                    Source = new ImageSource
                    {
                        MediaType = mediaType,
                        Data = Convert.ToBase64String(imageData)
                    }
                };
            }
            else
            {
                var content = await File.ReadAllTextAsync(fullPath);
                var fileName = Path.GetFileName(fullPath);
                return new TextContentBlock { Text = $"[File: {fileName}]\n{content}" };
            }
        }
        catch
        {
            return null;
        }
    }

    private static async Task SingleAskAsync(string question)
    {
        var storage = new CredentialStorage();
        var hasValid = await storage.HasValidCredentialsAsync();

        if (!hasValid)
        {
            Console.WriteLine("Not authenticated. Run \"login\" first.");
            Environment.Exit(1);
        }

        var client = new ClaudeClient();

        try
        {
            Console.Write("Claude: ");

            var fullResponse = new System.Text.StringBuilder();
            var startTime = DateTime.UtcNow;

            await foreach (var chunk in client.ChatStreamAsync([Message.User(question)]))
            {
                Console.Write(chunk);
                fullResponse.Append(chunk);
            }

            var elapsed = (DateTime.UtcNow - startTime).TotalSeconds;

            var response = await client.ChatAsync([Message.User(question)]);
            var inputTokens = response.Usage.InputTokens;
            var outputTokens = response.Usage.OutputTokens;
            var tokensPerSec = outputTokens / elapsed;

            Console.WriteLine("\n");
            Console.WriteLine($"[{inputTokens} in / {outputTokens} out | {tokensPerSec:F1} tok/s | {elapsed:F1}s]");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Environment.Exit(1);
        }
    }
}
