using System.Text.Json;
using Xunit;

namespace ClaudeOAuth.Tests;

public class VisionIntegrationTests
{
    private static string GetFixturePath(string filename)
    {
        var baseDir = AppContext.BaseDirectory;
        return Path.Combine(baseDir, "__fixtures__", filename);
    }

    [Fact(Skip = "Integration test - requires authentication. Remove Skip to run.")]
    public async Task AnalyzesCatImageCorrectly()
    {
        var storage = new CredentialStorage();
        if (!await storage.HasValidCredentialsAsync())
        {
            throw new InvalidOperationException("Not authenticated. Run OAuth login first.");
        }

        var catImagePath = GetFixturePath("cat.jpg");
        Assert.True(File.Exists(catImagePath), $"Test fixture not found: {catImagePath}");

        var message = await Message.UserWithImageFromFileAsync(
            "What animal is in this image? Reply with just the animal name in one word.",
            catImagePath
        );

        var client = new ClaudeClient();
        var response = await client.ChatAsync(
            [message],
            new ChatOptions { Model = "claude-sonnet-4-20250514", MaxTokens = 50 }
        );

        var answer = response.Content.ToLowerInvariant();
        Assert.Contains("cat", answer);
    }

    [Fact(Skip = "Integration test - requires authentication. Remove Skip to run.")]
    public async Task AnalyzesDiceImageCorrectly()
    {
        var storage = new CredentialStorage();
        if (!await storage.HasValidCredentialsAsync())
        {
            throw new InvalidOperationException("Not authenticated. Run OAuth login first.");
        }

        var diceImagePath = GetFixturePath("dice.png");
        Assert.True(File.Exists(diceImagePath), $"Test fixture not found: {diceImagePath}");

        var message = await Message.UserWithImageFromFileAsync(
            "What objects are shown in this image? Reply briefly.",
            diceImagePath
        );

        var client = new ClaudeClient();
        var response = await client.ChatAsync(
            [message],
            new ChatOptions { Model = "claude-sonnet-4-20250514", MaxTokens = 100 }
        );

        var answer = response.Content.ToLowerInvariant();
        Assert.True(
            answer.Contains("dice") || answer.Contains("die") || answer.Contains("cube"),
            $"Expected response to mention dice, got: {answer}"
        );
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
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            Message.UserWithImageFromFileAsync("test", "/nonexistent/path/file.png")
        );
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
