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

    private SqliteUsagePersistence CreatePersistence(bool includeTimings = true)
    {
        var config = Options.Create(new UsageConfig { Path = _tempDb, IncludeTimings = includeTimings });
        return new SqliteUsagePersistence(config);
    }

    private static SessionEntry MakeEntry(
        DateTimeOffset timestamp,
        string model,
        string route,
        TokenUsage? usage = null,
        DateTimeOffset? requestTime = null,
        DateTimeOffset? responseTime = null)
    {
        var now = requestTime ?? timestamp;
        var rt = responseTime ?? timestamp;
        usage ??= new TokenUsage();
        return new SessionEntry(
            Timestamp: timestamp,
            Model: model,
            Route: route,
            Usage: usage,
            RequestTime: now,
            ResponseTime: rt);
    }

    [Fact]
    public void Record_CreatesDatabaseAndInserts()
    {
        // Arrange
        var service = new UsageService(CreatePersistence());
        var date = new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero);

        // Act
        service.Record(MakeEntry(date, "llama3.2", "/v1/chat/completions",
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
        var service = new UsageService(CreatePersistence());
        var now = DateTimeOffset.UtcNow;

        // Act
        service.Record(MakeEntry(
            new DateTimeOffset(2026, 5, 1, 10, 0, 0, TimeSpan.Zero),
            "llama3.2", "/v1/chat/completions",
            new TokenUsage(10, 50, 60),
            now, now));
        service.Record(MakeEntry(
            new DateTimeOffset(2026, 5, 1, 10, 5, 0, TimeSpan.Zero),
            "mistral", "/v1/chat/completions",
            new TokenUsage(20, 100, 120),
            now, now));

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
        var service = new UsageService(CreatePersistence());
        var expected = new DateTimeOffset(2026, 5, 1, 14, 30, 0, TimeSpan.Zero);

        // Act
        service.Record(MakeEntry(expected, "test", "/v1/completions",
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
        var service = new UsageService(CreatePersistence(includeTimings: true));
        var now = DateTimeOffset.UtcNow;

        // Act
        service.Record(MakeEntry(
            new DateTimeOffset(2026, 5, 1, 10, 0, 0, TimeSpan.Zero),
            "llama3.2", "/v1/chat/completions",
            new TokenUsage(10, 50, 60, 120.5, 450.3, null, null, 15),
            now, now));

        // Assert
        using var conn = new SqliteConnection($"Data Source={_tempDb}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT prompt_ms, completion_ms, cache_hits, request_time, response_time, client_ip, status_code, request_headers FROM usage_records LIMIT 1";
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
        var service = new UsageService(CreatePersistence(includeTimings: false));
        var now = DateTimeOffset.UtcNow;

        // Act
        service.Record(MakeEntry(
            new DateTimeOffset(2026, 5, 1, 10, 0, 0, TimeSpan.Zero),
            "llama3.2", "/v1/chat/completions",
            new TokenUsage(10, 50, 60, 120.5, 450.3, null, null, 15),
            now, now));

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
        var service = new UsageService(CreatePersistence(includeTimings: true));
        var now = DateTimeOffset.UtcNow;

        // Act
        service.Record(MakeEntry(
            new DateTimeOffset(2026, 5, 1, 10, 0, 0, TimeSpan.Zero),
            "llama3.2", "/v1/chat/completions",
            new TokenUsage(10, 50, 60),
            now, now));

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
        var service = new UsageService(CreatePersistence());
        // Override the path for this test
        var persistence = (SqliteUsagePersistence)service.GetType().GetField("_persistence", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(service);
        if (persistence is SqliteUsagePersistence p)
        {
            var field = typeof(SqliteUsagePersistence).GetField("_connectionString", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field is not null)
                field.SetValue(p, $"Data Source={dbInNewDir}");
        }

        // Act
        service.Record(MakeEntry(DateTimeOffset.UtcNow, "test", "/v1/completions",
            new TokenUsage(1, 2, 3)));

        // Assert
        Assert.True(File.Exists(dbInNewDir));
    }

    [Fact]
    public void Record_StoresNewFields()
    {
        // Arrange
        var service = new UsageService(CreatePersistence());
        var requestTime = new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var responseTime = new DateTimeOffset(2026, 6, 1, 12, 0, 1, TimeSpan.Zero);
        var headers = "{\"X-Llama-Model\":\"llama3.2\",\"X-Llama-Temperature\":\"0.7\"}";

        // Act
        service.Record(new SessionEntry(
            Timestamp: requestTime,
            Model: "llama3.2",
            Route: "/v1/chat/completions",
            Usage: new TokenUsage(10, 50, 60),
            RequestTime: requestTime,
            ResponseTime: responseTime,
            ClientIp: "192.168.1.100",
            StatusCode: 200,
            RequestHeaders: headers));

        // Assert
        using var conn = new SqliteConnection($"Data Source={_tempDb}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT request_time, response_time, client_ip, status_code, request_headers FROM usage_records LIMIT 1";
        using var reader = cmd.ExecuteReader();
        reader.Read();

        Assert.Equal(requestTime.ToString("o"), reader.GetString(reader.GetOrdinal("request_time")));
        Assert.Equal(responseTime.ToString("o"), reader.GetString(reader.GetOrdinal("response_time")));
        Assert.Equal("192.168.1.100", reader.GetString(reader.GetOrdinal("client_ip")));
        Assert.Equal(200, reader.GetInt32(reader.GetOrdinal("status_code")));
        Assert.Equal(headers, reader.GetString(reader.GetOrdinal("request_headers")));
    }

    [Fact]
    public void Record_StoresStatusCode()
    {
        // Arrange
        var service = new UsageService(CreatePersistence());
        var now = DateTimeOffset.UtcNow;

        // Act
        service.Record(new SessionEntry(
            Timestamp: now,
            Model: "llama3.2",
            Route: "/v1/chat/completions",
            Usage: new TokenUsage(10, 50, 60),
            RequestTime: now,
            ResponseTime: now,
            StatusCode: 500));

        // Assert
        using var conn = new SqliteConnection($"Data Source={_tempDb}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT status_code FROM usage_records LIMIT 1";
        using var reader = cmd.ExecuteReader();
        reader.Read();
        Assert.Equal(500, reader.GetInt32(reader.GetOrdinal("status_code")));
    }

    public void Dispose()
    {
        if (File.Exists(_tempDb))
            File.Delete(_tempDb);
    }
}
