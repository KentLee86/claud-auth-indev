using System.Diagnostics;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace ClaudeOAuth.Tests;

public class VisionIntegrationTests
{
    private readonly ITestOutputHelper _output;

    public VisionIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static string GetFixturePath(string filename)
    {
        var baseDir = AppContext.BaseDirectory;
        return Path.Combine(baseDir, "__fixtures__", filename);
    }

    private void LogApiMetrics(ChatResponse response, TimeSpan elapsed, string testName)
    {
        var inputTokens = response.Usage.InputTokens;
        var outputTokens = response.Usage.OutputTokens;
        var totalTokens = inputTokens + outputTokens;
        var tokensPerSecond = outputTokens / elapsed.TotalSeconds;

        _output.WriteLine($"");
        _output.WriteLine($"=== {testName} API Metrics ===");
        _output.WriteLine($"Model: {response.Model}");
        _output.WriteLine($"Stop Reason: {response.StopReason}");
        _output.WriteLine($"");
        _output.WriteLine($"Token Usage:");
        _output.WriteLine($"  Input Tokens:  {inputTokens:N0}");
        _output.WriteLine($"  Output Tokens: {outputTokens:N0}");
        _output.WriteLine($"  Total Tokens:  {totalTokens:N0}");
        _output.WriteLine($"");
        _output.WriteLine($"Performance:");
        _output.WriteLine($"  Elapsed Time:     {elapsed.TotalSeconds:F2}s");
        _output.WriteLine($"  Tokens/Second:    {tokensPerSecond:F2}");
        _output.WriteLine($"  Time to First:    ~{elapsed.TotalMilliseconds:F0}ms (non-streaming)");
        _output.WriteLine($"");
        _output.WriteLine($"Response Preview:");
        _output.WriteLine($"  {(response.Content.Length > 100 ? response.Content[..100] + "..." : response.Content)}");
        _output.WriteLine($"================================");
    }

    private async Task<bool> EnsureAuthenticatedAsync()
    {
        var storage = new CredentialStorage();
        var hasAuth = await storage.HasValidCredentialsAsync();
        
        if (!hasAuth)
        {
            _output.WriteLine("⚠️  WARNING: No valid credentials found at ~/.claude-oauth/credentials.json");
            _output.WriteLine("   Run TypeScript OAuth login first: cd .. && bun run login");
            _output.WriteLine("   Skipping integration test...");
        }
        
        return hasAuth;
    }

    [Fact]
    public async Task AnalyzesCatImageCorrectly()
    {
        if (!await EnsureAuthenticatedAsync())
        {
            return;
        }

        var catImagePath = GetFixturePath("cat.jpg");
        Assert.True(File.Exists(catImagePath), $"Test fixture not found: {catImagePath}");

        var message = await Message.UserWithImageFromFileAsync(
            "What animal is in this image? Reply with just the animal name in one word.",
            catImagePath
        );

        var client = new ClaudeClient();
        var stopwatch = Stopwatch.StartNew();
        var response = await client.ChatAsync(
            [message],
            new ChatOptions { Model = "claude-sonnet-4-20250514", MaxTokens = 50 }
        );
        stopwatch.Stop();

        LogApiMetrics(response, stopwatch.Elapsed, "Cat Image Analysis");

        var answer = response.Content.ToLowerInvariant();
        Assert.Contains("cat", answer);
    }

    [Fact]
    public async Task AnalyzesDiceImageCorrectly()
    {
        if (!await EnsureAuthenticatedAsync())
        {
            return;
        }

        var diceImagePath = GetFixturePath("dice.png");
        Assert.True(File.Exists(diceImagePath), $"Test fixture not found: {diceImagePath}");

        var message = await Message.UserWithImageFromFileAsync(
            "What objects are shown in this image? Reply briefly.",
            diceImagePath
        );

        var client = new ClaudeClient();
        var stopwatch = Stopwatch.StartNew();
        var response = await client.ChatAsync(
            [message],
            new ChatOptions { Model = "claude-sonnet-4-20250514", MaxTokens = 100 }
        );
        stopwatch.Stop();

        LogApiMetrics(response, stopwatch.Elapsed, "Dice Image Analysis");

        var answer = response.Content.ToLowerInvariant();
        Assert.True(
            answer.Contains("dice") || answer.Contains("die") || answer.Contains("cube"),
            $"Expected response to mention dice, got: {answer}"
        );
    }

    [Fact]
    public async Task AnalyzesTextFileContent()
    {
        if (!await EnsureAuthenticatedAsync())
        {
            return;
        }

        var textFilePath = GetFixturePath("sample.txt");
        Assert.True(File.Exists(textFilePath), $"Test fixture not found: {textFilePath}");

        var textContent = await File.ReadAllTextAsync(textFilePath);
        var message = Message.User($"Summarize this text in one sentence:\n\n{textContent}");

        var client = new ClaudeClient();
        var stopwatch = Stopwatch.StartNew();
        var response = await client.ChatAsync(
            [message],
            new ChatOptions { Model = "claude-sonnet-4-20250514", MaxTokens = 100 }
        );
        stopwatch.Stop();

        LogApiMetrics(response, stopwatch.Elapsed, "Text File Analysis");

        Assert.NotEmpty(response.Content);
    }

    [Fact]
    public async Task MessageUserWithImageFromFile_LoadsCatJpgCorrectly()
    {
        var catImagePath = GetFixturePath("cat.jpg");
        if (!File.Exists(catImagePath)) return;

        var message = await Message.UserWithImageFromFileAsync("Describe this", catImagePath);

        Assert.Equal("user", message.Role);
        var content = Assert.IsType<MessageContentBlock[]>(message.Content);
        Assert.Equal(2, content.Length);

        var imageBlock = Assert.IsType<ImageContentBlock>(content[0]);
        Assert.Equal("image/jpeg", imageBlock.Source.MediaType);
        Assert.True(imageBlock.Source.Data.Length > 1000);

        var textBlock = Assert.IsType<TextContentBlock>(content[1]);
        Assert.Equal("Describe this", textBlock.Text);

        _output.WriteLine($"Cat image loaded: {imageBlock.Source.Data.Length} base64 chars");
    }

    [Fact]
    public async Task MessageUserWithImageFromFile_LoadsSamplePngCorrectly()
    {
        var sampleImagePath = GetFixturePath("sample.png");
        if (!File.Exists(sampleImagePath)) return;

        var message = await Message.UserWithImageFromFileAsync("What is this?", sampleImagePath);

        Assert.Equal("user", message.Role);
        var content = Assert.IsType<MessageContentBlock[]>(message.Content);
        Assert.Equal(2, content.Length);

        var imageBlock = Assert.IsType<ImageContentBlock>(content[0]);
        Assert.Equal("image/png", imageBlock.Source.MediaType);
        Assert.Equal("base64", imageBlock.Source.Type);
        Assert.NotEmpty(imageBlock.Source.Data);

        _output.WriteLine($"Sample PNG loaded: {imageBlock.Source.Data.Length} base64 chars");
    }

    [Fact]
    public async Task MessageUserWithImageFromFile_LoadsDicePngCorrectly()
    {
        var diceImagePath = GetFixturePath("dice.png");
        if (!File.Exists(diceImagePath)) return;

        var message = await Message.UserWithImageFromFileAsync("Count the dice", diceImagePath);

        var content = Assert.IsType<MessageContentBlock[]>(message.Content);
        var imageBlock = Assert.IsType<ImageContentBlock>(content[0]);
        Assert.Equal("image/png", imageBlock.Source.MediaType);
        Assert.NotEmpty(imageBlock.Source.Data);

        _output.WriteLine($"Dice PNG loaded: {imageBlock.Source.Data.Length} base64 chars");
    }

    [Fact]
    public void LoadTextFile_CreatesTextContentBlock()
    {
        var textFilePath = GetFixturePath("sample.txt");
        if (!File.Exists(textFilePath)) return;

        var text = File.ReadAllText(textFilePath);
        var block = new TextContentBlock { Text = text };

        Assert.Contains("Hello from test file", block.Text);
        Assert.Contains("sample text file", block.Text);

        _output.WriteLine($"Text file loaded: {text.Length} chars, {text.Split('\n').Length} lines");
    }

    [Fact]
    public void GetMediaTypeFromExtension_ReturnsCorrectTypes()
    {
        Assert.Equal("image/jpeg", GetMediaType(".jpg"));
        Assert.Equal("image/jpeg", GetMediaType(".jpeg"));
        Assert.Equal("image/jpeg", GetMediaType(".JPG"));
        Assert.Equal("image/png", GetMediaType(".png"));
        Assert.Equal("image/png", GetMediaType(".PNG"));
        Assert.Equal("image/gif", GetMediaType(".gif"));
        Assert.Equal("image/webp", GetMediaType(".webp"));
        Assert.Equal("image/png", GetMediaType(".unknown"));
        Assert.Equal("image/png", GetMediaType(".bmp"));
    }

    [Fact]
    public async Task LoadNonExistentFile_ThrowsException()
    {
        var exception = await Assert.ThrowsAnyAsync<IOException>(() =>
            Message.UserWithImageFromFileAsync("test", "/nonexistent/path/file.png")
        );
        _output.WriteLine($"Expected exception thrown: {exception.GetType().Name}");
    }

    [Fact]
    public void TextContentBlock_SerializesWithCorrectType()
    {
        var block = new TextContentBlock { Text = "Hello from test file" };
        var json = JsonSerializer.Serialize<MessageContentBlock>(block);

        Assert.Contains("\"type\":\"text\"", json);
        Assert.Contains("\"text\":\"Hello from test file\"", json);
    }

    [Fact]
    public void ImageContentBlock_SerializesWithCorrectStructure()
    {
        var block = new ImageContentBlock
        {
            Source = new ImageSource { MediaType = "image/jpeg", Data = "base64data" }
        };
        var json = JsonSerializer.Serialize<MessageContentBlock>(block);

        Assert.Contains("\"type\":\"image\"", json);
        Assert.Contains("\"source\"", json);
        Assert.Contains("\"type\":\"base64\"", json);
        Assert.Contains("\"media_type\":\"image/jpeg\"", json);
        Assert.Contains("\"data\":\"base64data\"", json);
    }

    [Fact]
    public void Message_WithMixedContent_SerializesCorrectly()
    {
        var message = Message.User(
            new ImageContentBlock
            {
                Source = new ImageSource { MediaType = "image/png", Data = "imagedata" }
            },
            new TextContentBlock { Text = "What is this?" }
        );

        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var json = JsonSerializer.Serialize(message, options);

        Assert.Contains("\"role\":\"user\"", json);
        Assert.Contains("\"type\":\"image\"", json);
        Assert.Contains("\"type\":\"text\"", json);
        Assert.Contains("imagedata", json);
        Assert.Contains("What is this?", json);
    }

    private static string GetMediaType(string extension) => extension.ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        _ => "image/png"
    };
}
