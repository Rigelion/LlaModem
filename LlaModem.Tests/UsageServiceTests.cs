using LlaModem.Config;
using LlaModem.Models;
using LlaModem.Services;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace LlaModem.Tests;

public class UsageServiceTests : IDisposable
{
    private readonly string _tempDb;

    public UsageServiceTests()
    {
        _tempDb = Path.Combine(Path.GetTempPath(), $"usage-test-{Guid.NewGuid()}.db");
    }

    private IOptions<UsageConfig> CreateOptions(bool includeTimings = true)
    {
        return Options.Create(new UsageConfig { Path = _tempDb, IncludeTimings = includeTimings });
    }

    private List<SqliteDataReader> Query(string sql)
    {
        using var conn = new SqliteConnection($"Data Source={_tempDb}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        return new List<SqliteDataReader> { cmd.ExecuteReader() };
    }

    private static T ReadValue<T>(SqliteDataReader reader, string columnName)
    {
        reader.Read();
        var idx = reader.GetOrdinal(columnName);
        return reader.IsDBNull(idx) ? default! : reader.GetValue(idx) is T v ? v : throw new InvalidOperationException();
    }

    [Fact]
    public void Record_CreatesDatabaseAndInserts()
    {
        // Arrange
        var config = CreateOptions();
        var service = new UsageService(config);
        var date = new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero);

        // Act
        service.Record(new SessionEntry(date, "llama3.2", "/v1/chat/completions",
            new TokenUsage(10, 50, 60)));

        // Assert
        Assert.True(File.Exists(_tempDb));

        using var conn = new SqliteConnection($"Data Source={_tempDb}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT model, route, prompt_tokens, completion_tokens, total_tokens FROM usage_records LIMIT 1";
        using var reader = cmd.ExecuteReader();
        reader.Read();
        Assert.Equal("llama3.2", reader.GetString(reader.GetOrdinal("model")));
        Assert.Equal("/v1/chat/completions", reader.GetString(reader.GetOrdinal("route")));
        Assert.Equal(10, reader.GetInt32(reader.GetOrdinal("prompt_tokens")));
        Assert.Equal(50, reader.GetInt32(reader.GetOrdinal("completion_tokens")));
        Assert.Equal(60, reader.GetInt32(reader.GetOrdinal("total_tokens")));
    }

    [Fact]
    public void Record_AppendsMultipleEntries()
    {
        // Arrange
        var config = CreateOptions();
        var service = new UsageService(config);

        // Act
        service.Record(new SessionEntry(new DateTimeOffset(2026, 5, 1, 10, 0, 0, TimeSpan.Zero), "llama3.2", "/v1/chat/completions",
            new TokenUsage(10, 50, 60)));
        service.Record(new SessionEntry(new DateTimeOffset(2026, 5, 1, 10, 5, 0, TimeSpan.Zero), "mistral", "/v1/chat/completions",
            new TokenUsage(20, 100, 120)));

        // Assert
        using var conn = new SqliteConnection($"Data Source={_tempDb}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT model FROM usage_records ORDER BY timestamp ASC";
        using var reader = cmd.ExecuteReader();
        var models = new List<string>();
        while (reader.Read())
            models.Add(reader.GetString(reader.GetOrdinal("model")));

        Assert.Equal(2, models.Count);
        Assert.Equal("llama3.2", models[0]);
        Assert.Equal("mistral", models[1]);
    }

    [Fact]
    public void Record_StoresTimestampCorrectly()
    {
        // Arrange
        var config = CreateOptions();
        var service = new UsageService(config);
        var expected = new DateTimeOffset(2026, 5, 1, 14, 30, 0, TimeSpan.Zero);

        // Act
        service.Record(new SessionEntry(expected, "test", "/v1/completions",
            new TokenUsage(5, 25, 30)));

        // Assert
        using var conn = new SqliteConnection($"Data Source={_tempDb}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT timestamp FROM usage_records LIMIT 1";
        using var reader = cmd.ExecuteReader();
        reader.Read();
        var stored = DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("timestamp")));
        Assert.Equal(expected, stored);
    }

    [Fact]
    public void Record_StoresTimings_WhenEnabled()
    {
        // Arrange
        var config = CreateOptions(includeTimings: true);
        var service = new UsageService(config);

        // Act
        service.Record(new SessionEntry(
            new DateTimeOffset(2026, 5, 1, 10, 0, 0, TimeSpan.Zero),
            "llama3.2", "/v1/chat/completions",
            new TokenUsage(10, 50, 60, 120.5, 450.3, null, null, 15)));

        // Assert
        using var conn = new SqliteConnection($"Data Source={_tempDb}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT prompt_ms, completion_ms, cache_hits FROM usage_records LIMIT 1";
        using var reader = cmd.ExecuteReader();
        reader.Read();
        Assert.Equal(120.5, reader.GetDouble(reader.GetOrdinal("prompt_ms")));
        Assert.Equal(450.3, reader.GetDouble(reader.GetOrdinal("completion_ms")));
        Assert.Equal(15, reader.GetInt32(reader.GetOrdinal("cache_hits")));
    }

    [Fact]
    public void Record_OmitsTimings_WhenDisabled()
    {
        // Arrange
        var config = CreateOptions(includeTimings: false);
        var service = new UsageService(config);

        // Act
        service.Record(new SessionEntry(
            new DateTimeOffset(2026, 5, 1, 10, 0, 0, TimeSpan.Zero),
            "llama3.2", "/v1/chat/completions",
            new TokenUsage(10, 50, 60, 120.5, 450.3, null, null, 15)));

        // Assert
        using var conn = new SqliteConnection($"Data Source={_tempDb}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT prompt_ms, completion_ms, cache_hits FROM usage_records LIMIT 1";
        using var reader = cmd.ExecuteReader();
        reader.Read();
        Assert.True(reader.IsDBNull(reader.GetOrdinal("prompt_ms")));
        Assert.True(reader.IsDBNull(reader.GetOrdinal("completion_ms")));
        Assert.True(reader.IsDBNull(reader.GetOrdinal("cache_hits")));
    }

    [Fact]
    public void Record_HandlesNullTimings_WhenEnabled()
    {
        // Arrange
        var config = CreateOptions(includeTimings: true);
        var service = new UsageService(config);

        // Act
        service.Record(new SessionEntry(
            new DateTimeOffset(2026, 5, 1, 10, 0, 0, TimeSpan.Zero),
            "llama3.2", "/v1/chat/completions",
            new TokenUsage(10, 50, 60)));

        // Assert
        using var conn = new SqliteConnection($"Data Source={_tempDb}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT prompt_ms, completion_ms, cache_hits FROM usage_records LIMIT 1";
        using var reader = cmd.ExecuteReader();
        reader.Read();
        Assert.True(reader.IsDBNull(reader.GetOrdinal("prompt_ms")));
        Assert.True(reader.IsDBNull(reader.GetOrdinal("completion_ms")));
        Assert.True(reader.IsDBNull(reader.GetOrdinal("cache_hits")));
    }

    [Fact]
    public void Record_CreatesDatabaseFile_WhenDirectoryDoesNotExist()
    {
        // Arrange
        var dbInNewDir = Path.Combine(Path.GetTempPath(), $"usage-test-nested-{Guid.NewGuid()}", "sub", "db.db");
        var config = Options.Create(new UsageConfig { Path = dbInNewDir });
        var service = new UsageService(config);

        // Act
        service.Record(new SessionEntry(DateTimeOffset.UtcNow, "test", "/v1/completions",
            new TokenUsage(1, 2, 3)));

        // Assert
        Assert.True(File.Exists(dbInNewDir));
    }

    public void Dispose()
    {
        if (File.Exists(_tempDb))
            File.Delete(_tempDb);
    }
}
