using LlaModem.Config;
using LlaModem.Models;
using LlaModem.Services;
using Microsoft.Extensions.Options;

namespace LlaModem.Tests;

public class UsageServiceTests : IDisposable
{
    private readonly string _tempDir;

    public UsageServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"usage-test-{Guid.NewGuid()}");
    }

    [Fact]
    public void Record_CreatesDailyFile()
    {
        // Arrange
        var config = Options.Create(new UsageConfig { Path = _tempDir, FilenamePattern = "usage-{date}.md" });
        var service = new UsageService(config);
        var date = new DateTime(2026, 5, 1);

        // Act
        service.Record(new SessionEntry(date, "llama3.2", "/v1/chat/completions",
            new TokenUsage(10, 50, 60)));

        // Assert
        var filePath = Path.Combine(_tempDir, "usage-2026-05-01.md");
        Assert.True(File.Exists(filePath));

        var content = File.ReadAllText(filePath);
        Assert.Contains("Usage Report", content);
        Assert.Contains("llama3.2", content);
        Assert.Contains("10", content);
        Assert.Contains("50", content);
        Assert.Contains("60", content);
    }

    [Fact]
    public void Record_AppendsMultipleEntries()
    {
        // Arrange
        var config = Options.Create(new UsageConfig { Path = _tempDir, FilenamePattern = "usage-{date}.md" });
        var service = new UsageService(config);

        // Act
        service.Record(new SessionEntry(new DateTime(2026, 5, 1, 10, 0, 0), "llama3.2", "/v1/chat/completions",
            new TokenUsage(10, 50, 60)));
        service.Record(new SessionEntry(new DateTime(2026, 5, 1, 10, 5, 0), "mistral", "/v1/chat/completions",
            new TokenUsage(20, 100, 120)));

        // Assert
        var filePath = Path.Combine(_tempDir, "usage-2026-05-01.md");
        var content = File.ReadAllText(filePath);
        Assert.Matches(@"\|.*llama3.2.*", content);
        Assert.Matches(@"\|.*mistral.*", content);
    }

    [Fact]
    public void Record_CreatesSeparateFilesPerDay()
    {
        // Arrange
        var config = Options.Create(new UsageConfig { Path = _tempDir, FilenamePattern = "usage-{date}.md" });
        var service = new UsageService(config);

        // Act
        service.Record(new SessionEntry(new DateTime(2026, 5, 1), "llama3.2", "/v1/chat/completions",
            new TokenUsage(10, 50, 60)));
        service.Record(new SessionEntry(new DateTime(2026, 5, 2), "mistral", "/v1/chat/completions",
            new TokenUsage(20, 100, 120)));

        // Assert
        Assert.True(File.Exists(Path.Combine(_tempDir, "usage-2026-05-01.md")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "usage-2026-05-02.md")));
    }

    [Fact]
    public void Record_UsesAbsolutePath_WhenConfigured()
    {
        // Arrange
        var config = Options.Create(new UsageConfig { Path = _tempDir, FilenamePattern = "usage-{date}.md" });
        var service = new UsageService(config);

        // Act
        service.Record(new SessionEntry(DateTime.UtcNow, "test", "/v1/chat/completions",
            new TokenUsage(1, 2, 3)));

        // Assert
        Assert.True(Directory.Exists(_tempDir));
    }

    [Fact]
    public void Record_CreatesDirectory_IfNotExists()
    {
        // Arrange
        var nestedPath = Path.Combine(_tempDir, "nested", "deep");
        var config = Options.Create(new UsageConfig { Path = nestedPath, FilenamePattern = "usage-{date}.md" });
        var service = new UsageService(config);

        // Act
        service.Record(new SessionEntry(DateTime.UtcNow, "test", "/v1/chat/completions",
            new TokenUsage(1, 2, 3)));

        // Assert
        Assert.True(Directory.Exists(nestedPath));
    }

    [Fact]
    public void Record_IncludesRouteInOutput()
    {
        // Arrange
        var config = Options.Create(new UsageConfig { Path = _tempDir, FilenamePattern = "usage-{date}.md" });
        var service = new UsageService(config);

        // Act
        service.Record(new SessionEntry(DateTime.UtcNow, "test-model", "/v1/completions",
            new TokenUsage(5, 25, 30)));

        // Assert
        var content = File.ReadAllText(Path.Combine(_tempDir, $"usage-{DateTime.UtcNow:yyyy-MM-dd}.md"));
        Assert.Contains("/v1/completions", content);
    }

    [Fact]
    public void Record_IncludesTimingColumns_WhenEnabled()
    {
        // Arrange
        var config = Options.Create(new UsageConfig { Path = _tempDir, FilenamePattern = "usage-{date}.md", IncludeTimings = true });
        var service = new UsageService(config);

        // Act
        service.Record(new SessionEntry(new DateTime(2026, 5, 1, 10, 0, 0), "llama3.2", "/v1/chat/completions",
            new TokenUsage(10, 50, 60, 120.5, 450.3, null, null, 15)));

        // Assert
        var filePath = Path.Combine(_tempDir, "usage-2026-05-01.md");
        var content = File.ReadAllText(filePath);
        Assert.Contains("Prompt Ms", content);
        Assert.Contains("Completion Ms", content);
        Assert.Contains("Cache Hits", content);
        Assert.Contains("120.5", content);
        Assert.Contains("450.3", content);
        Assert.Contains("15", content);
    }

    [Fact]
    public void Record_OmitsTimingColumns_WhenDisabled()
    {
        // Arrange
        var config = Options.Create(new UsageConfig { Path = _tempDir, FilenamePattern = "usage-{date}.md", IncludeTimings = false });
        var service = new UsageService(config);

        // Act
        service.Record(new SessionEntry(new DateTime(2026, 5, 1, 10, 0, 0), "llama3.2", "/v1/chat/completions",
            new TokenUsage(10, 50, 60)));

        // Assert
        var filePath = Path.Combine(_tempDir, "usage-2026-05-01.md");
        var content = File.ReadAllText(filePath);
        Assert.DoesNotContain("Prompt Ms", content);
        Assert.DoesNotContain("Completion Ms", content);
        Assert.DoesNotContain("Cache Hits", content);
    }

    [Fact]
    public void Record_HandlesNullTimingsInOutput()
    {
        // Arrange
        var config = Options.Create(new UsageConfig { Path = _tempDir, FilenamePattern = "usage-{date}.md", IncludeTimings = true });
        var service = new UsageService(config);

        // Act
        service.Record(new SessionEntry(new DateTime(2026, 5, 1, 10, 0, 0), "llama3.2", "/v1/chat/completions",
            new TokenUsage(10, 50, 60)));

        // Assert
        var filePath = Path.Combine(_tempDir, "usage-2026-05-01.md");
        var content = File.ReadAllText(filePath);
        // Header should have timing columns
        Assert.Contains("Prompt Ms", content);
        // Data row should have empty timing cells (no values between the last total tokens and final |)
        var lines = content.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var dataLine = lines.First(l => l.Contains("llama3.2"));
        // Data row should have timing columns with empty values
        Assert.Contains("llama3.2", dataLine);
        Assert.Contains("| 10 | 50 | 60 |", dataLine);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }
}
