# AGENTS.md - ClaudeOAuth .NET Library

Guidelines for AI coding agents working in this repository.

## Project Overview

A .NET 9.0 OAuth client library for the Claude API using PKCE flow. Shares credentials with the TypeScript version at `~/.claude-oauth/credentials.json`.

## Build Commands

```bash
dotnet build                      # Build solution
dotnet build -c Release           # Release build
dotnet restore                    # Restore packages
dotnet pack ClaudeOAuth -c Release  # Create NuGet package
```

## Test Commands

```bash
# Run all tests
dotnet test

# Run with detailed output
dotnet test --logger "console;verbosity=detailed"

# Run a single test by name
dotnet test --filter "AnalyzesCatImageCorrectly"

# Run tests in a specific class
dotnet test --filter "VisionIntegrationTests"
dotnet test --filter "PkceTests"

# Run tests matching a pattern
dotnet test --filter "FullyQualifiedName~Vision"

# Exclude integration tests (fast)
dotnet test --filter "FullyQualifiedName!~Analyzes"
```

### Just Commands

```bash
just test          # All tests
just test-vision   # Vision integration tests
just test-cat      # Cat image test
just quick         # Fast unit tests only
just check-auth    # Check authentication
```

## Project Structure

```
ClaudeOAuth/
├── Constants.cs    # OAuth endpoints, client ID
├── Models.cs       # DTOs (Message, ChatResponse, ContentBlocks)
├── Pkce.cs         # PKCE code verifier/challenge
├── Storage.cs      # Credential storage (~/.claude-oauth/)
├── OAuth.cs        # OAuth flow (token exchange, refresh)
└── Client.cs       # API client (Chat, ChatStream, Ask)

ClaudeOAuth.Tests/
├── __fixtures__/   # Test files (cat.jpg, dice.png, sample.txt)
└── *Tests.cs       # Unit and integration tests
```

## Code Style Guidelines

### Configuration
- Target: .NET 9.0, C# latest
- Nullable: enabled
- Implicit usings: enabled

### Naming Conventions

| Element | Convention | Example |
|---------|------------|---------|
| Classes/Records | PascalCase | `ClaudeClient`, `ChatResponse` |
| Interfaces | IPascalCase | `ICredentialStorage` |
| Methods | PascalCase | `ChatAsync` |
| Private fields | _camelCase | `_httpClient` |
| Local/params | camelCase | `accessToken` |

### Type Definitions

```csharp
// Use records for DTOs with required properties
public record ChatResponse
{
    public required string Id { get; init; }
    public string? OptionalField { get; init; }  // nullable for optional
}

// Use JsonPropertyName for snake_case API fields
[JsonPropertyName("input_tokens")]
public required int InputTokens { get; init; }
```

### Async Patterns

```csharp
// Async suffix for async methods
public async Task<ChatResponse> ChatAsync(...)

// IAsyncEnumerable for streaming
public async IAsyncEnumerable<string> ChatStreamAsync(
    ...,
    [EnumeratorCancellation] CancellationToken ct = default)
```

### Error Handling

```csharp
// Descriptive exceptions with status code
if (!response.IsSuccessStatusCode)
{
    var error = await response.Content.ReadAsStringAsync();
    throw new HttpRequestException($"Failed: {(int)response.StatusCode} - {error}");
}

// Null-coalescing throw
var data = await response.Content.ReadFromJsonAsync<T>()
    ?? throw new JsonException("Failed to parse");
```

### JSON Serialization

```csharp
// JsonSerializerOptions configuration
private static readonly JsonSerializerOptions JsonOptions = new()
{
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
};

// Polymorphic types with discriminator
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TextContentBlock), "text")]
[JsonDerivedType(typeof(ImageContentBlock), "image")]
public abstract record MessageContentBlock;
```

### Testing

```csharp
[Fact]
public async Task MethodName_Condition_ExpectedResult()
{
    // Use ITestOutputHelper for logging
    _output.WriteLine($"Result: {result}");
}

// Mock HttpClient with Moq
mockHandler.Protected()
    .Setup<Task<HttpResponseMessage>>("SendAsync", ...)

// Skip if not authenticated
if (!await EnsureAuthenticatedAsync()) return;
```

## Things to Avoid

- `dynamic` types
- Suppressing nullable warnings without reason
- `async void` (except event handlers)
- Blocking calls (`.Result`, `.Wait()`) in async code
- Committing credentials

## Authentication

Credentials stored at `~/.claude-oauth/credentials.json`. To authenticate:

```bash
cd .. && bun run login  # Use TypeScript OAuth login
```
