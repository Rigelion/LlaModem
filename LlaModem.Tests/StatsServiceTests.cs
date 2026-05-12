using LlaModem.Models;
using LlaModem.Services;
using Microsoft.Data.Sqlite;

namespace LlaModem.Tests;

public class StatsServiceTests : IDisposable
{
    private readonly string _tempDb;

    public StatsServiceTests() =>
        _tempDb = Path.Combine(Path.GetTempPath(), $"stats-test-{Guid.NewGuid()}.db");

    private string ConnString => $"Data Source={_tempDb}";

    private StatsService CreateService() =>
        new(new SqliteUsagePersistence(new Microsoft.Extensions.Options.OptionsWrapper<LlaModem.Config.UsageConfig>(new LlaModem.Config.UsageConfig { Path = _tempDb })));

    private async Task SeedDataAsync(params (string Timestamp, string Model, string Route, int PromptTokens, int CompletionTokens, int TotalTokens, double? PromptMs, double? CompletionMs, int? CacheHits, int StatusCode, string? ClientIp)[] records)
    {
        // Database already exists from previous test; using fresh connection ensures clean state
        await using var conn = new SqliteConnection(ConnString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS usage_records (
                id INTEGER PRIMARY KEY AUTOINCREMENT, timestamp TEXT NOT NULL, request_id TEXT,
                model TEXT NOT NULL, route TEXT NOT NULL, prompt_tokens INTEGER NOT NULL DEFAULT 0,
                completion_tokens INTEGER NOT NULL DEFAULT 0, total_tokens INTEGER NOT NULL DEFAULT 0,
                prompt_ms REAL, completion_ms REAL, prompt_per_token_ms REAL, completion_per_token_ms REAL,
                cache_hits INTEGER, cached_tokens INTEGER, request_time TEXT, response_time TEXT,
                client_ip TEXT, status_code INTEGER NOT NULL DEFAULT 200, request_headers TEXT
            );
            CREATE INDEX IF NOT EXISTS idx_ts ON usage_records(timestamp);
            CREATE INDEX IF NOT EXISTS idx_model ON usage_records(model);
            """;
        await cmd.ExecuteNonQueryAsync();

        await using var ins = conn.CreateCommand();
        ins.CommandText = """
            INSERT INTO usage_records (timestamp, request_id, model, route, prompt_tokens, completion_tokens,
                total_tokens, prompt_ms, completion_ms, prompt_per_token_ms, completion_per_token_ms,
                cache_hits, cached_tokens, request_time, response_time, client_ip, status_code, request_headers)
            VALUES (@ts, NULL, @model, @route, @pt, @ct, @tt, @pm, @cm, NULL, NULL, @ch, NULL, NULL, NULL, @cip, @sc, NULL)
            """;

        foreach (var r in records)
        {
            ins.Parameters.Clear();
            ins.Parameters.AddWithValue("@ts", r.Timestamp);
            ins.Parameters.AddWithValue("@model", r.Model);
            ins.Parameters.AddWithValue("@route", r.Route);
            ins.Parameters.AddWithValue("@pt", r.PromptTokens);
            ins.Parameters.AddWithValue("@ct", r.CompletionTokens);
            ins.Parameters.AddWithValue("@tt", r.TotalTokens);
            ins.Parameters.AddWithValue("@pm", r.PromptMs ?? (object)DBNull.Value);
            ins.Parameters.AddWithValue("@cm", r.CompletionMs ?? (object)DBNull.Value);
            ins.Parameters.AddWithValue("@ch", r.CacheHits ?? (object)DBNull.Value);
            ins.Parameters.AddWithValue("@cip", r.ClientIp ?? (object)DBNull.Value);
            ins.Parameters.AddWithValue("@sc", r.StatusCode);
            await ins.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task GetDailyUsageAsync_EmptyDatabase_ReturnsEmptyResult()
    {
        // Database already exists from previous test; using fresh connection ensures clean state
        // Create table with empty data
        await SeedDataAsync();
        var result = await CreateService().GetDailyUsageAsync(30, null);
        Assert.Empty(result.Daily);
        Assert.Equal(0, result.Summary.TotalRequests);
        Assert.Equal(0.0, result.Summary.CacheHitRate);
    }

    [Fact]
    public async Task GetDailyUsageAsync_SingleDay_ReturnsAggregatedRow()
    {
        await SeedDataAsync(
            ("2026-04-15T10:00:00", "llama3.2", "/v1/chat/completions", 10, 50, 60, 100.0, 200.0, 0, 200, "10.0.0.1"),
            ("2026-04-15T11:00:00", "llama3.2", "/v1/chat/completions", 20, 100, 120, 150.0, 300.0, 0, 200, "10.0.0.2"));
        var persistence = new SqliteUsagePersistence(new Microsoft.Extensions.Options.OptionsWrapper<LlaModem.Config.UsageConfig>(new LlaModem.Config.UsageConfig { Path = _tempDb }));
        var result = await new StatsService(persistence).GetDailyUsageAsync(30, null);

        Assert.Single(result.Daily);
        var r = result.Daily[0];
        Assert.Equal("2026-04-15", r.Date);
        Assert.Equal("llama3.2", r.Model);
        Assert.Equal(2, r.Requests);
        Assert.Equal(30, r.PromptTokens);
        Assert.Equal(150, r.CompletionTokens);
        Assert.Equal(180, r.TotalTokens);
        Assert.Equal(125.0, r.AvgPromptMs);
        Assert.Equal(250.0, r.AvgCompletionMs);
        Assert.Equal(0.0, r.CacheHitRate);
    }

    [Fact]
    public async Task GetDailyUsageAsync_MultipleModels_SeparateRows()
    {
        await SeedDataAsync(
            ("2026-04-15T10:00:00", "llama3.2", "/v1/chat/completions", 10, 50, 60, 100.0, 200.0, 0, 200, null),
            ("2026-04-15T11:00:00", "mistral", "/v1/chat/completions", 20, 100, 120, 150.0, 300.0, 0, 200, null));
        var result = await CreateService().GetDailyUsageAsync(30, null);
        Assert.Equal(2, result.Daily.Length);
        Assert.Single(result.Daily, r => r.Model == "llama3.2");
        Assert.Single(result.Daily, r => r.Model == "mistral");
    }

    [Fact]
    public async Task GetDailyUsageAsync_MultipleDays_ReturnsOrderedRows()
    {
        await SeedDataAsync(
            ("2026-04-15T10:00:00", "llama3.2", "/v1/chat/completions", 10, 50, 60, 100.0, 200.0, 0, 200, null),
            ("2026-04-16T10:00:00", "llama3.2", "/v1/chat/completions", 20, 100, 120, 150.0, 300.0, 0, 200, null),
            ("2026-04-17T10:00:00", "llama3.2", "/v1/chat/completions", 30, 150, 180, 200.0, 400.0, 0, 200, null));
        var result = await CreateService().GetDailyUsageAsync(30, null);
        Assert.Equal(3, result.Daily.Length);
        // SQL orders by date DESC, so newest first
        Assert.Equal("2026-04-17", result.Daily[0].Date);
        Assert.Equal("2026-04-16", result.Daily[1].Date);
        Assert.Equal("2026-04-15", result.Daily[2].Date);
    }



    [Fact]
    public async Task GetDailyUsageAsync_NoCacheHits_ReturnsRate0()
    {
        await SeedDataAsync(
            ("2026-04-15T10:00:00", "llama3.2", "/v1/chat/completions", 10, 50, 60, 100.0, 200.0, 0, 200, null),
            ("2026-04-15T11:00:00", "llama3.2", "/v1/chat/completions", 20, 100, 120, 150.0, 300.0, 0, 200, null));
        var result = await CreateService().GetDailyUsageAsync(30, null);
        Assert.Equal(0.0, result.Daily[0].CacheHitRate);
        Assert.Equal(0.0, result.Summary.CacheHitRate);
    }

    [Fact]
    public async Task GetDailyUsageAsync_Period_ReturnsCorrectDates()
    {
        await SeedDataAsync(
            ("2026-04-15T10:00:00", "llama3.2", "/v1/chat/completions", 10, 50, 60, 100.0, 200.0, 0, 200, null));
        var result = await CreateService().GetDailyUsageAsync(7, null);
        var expectedFrom = DateTimeOffset.UtcNow.AddDays(-7).ToString("yyyy-MM-dd");
        Assert.Equal(expectedFrom, result.Period.From);
        Assert.NotNull(result.Period.To);
    }

    [Fact]
    public async Task GetRecentRequestsAsync_EmptyDatabase_ReturnsEmpty()
    {
        // Database already exists from previous test; using fresh connection ensures clean state
        var result = await CreateService().GetRecentRequestsAsync(10, 0, null);
        Assert.Equal(0, result.Total);
        Assert.Equal(0, result.Offset);
        Assert.Equal(10, result.Limit);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task GetRecentRequestsAsync_ReturnsMostRecentFirst()
    {
        await SeedDataAsync(
            ("2026-04-15T10:00:00", "llama3.2", "/v1/chat/completions", 10, 50, 60, 100.0, 200.0, 0, 200, "10.0.0.1"),
            ("2026-04-17T10:00:00", "mistral", "/v1/chat/completions", 20, 100, 120, 150.0, 300.0, 0, 200, "10.0.0.2"),
            ("2026-04-16T10:00:00", "llama3.2", "/v1/chat/completions", 30, 150, 180, 200.0, 400.0, 0, 200, "10.0.0.3"));
        var result = await CreateService().GetRecentRequestsAsync(10, 0, null);
        Assert.Equal(3, result.Total);
        Assert.Equal(3, result.Items.Length);
        Assert.Equal("2026-04-17", result.Items[0].Timestamp.Substring(0, 10));
        Assert.Equal("2026-04-16", result.Items[1].Timestamp.Substring(0, 10));
        Assert.Equal("2026-04-15", result.Items[2].Timestamp.Substring(0, 10));
    }

    [Fact]
    public async Task GetRecentRequestsAsync_Pagination_LimitAndOffset()
    {
        await SeedDataAsync(
            ("2026-04-15T10:00:00", "llama3.2", "/v1/chat/completions", 10, 50, 60, null, null, null, 200, null),
            ("2026-04-16T10:00:00", "llama3.2", "/v1/chat/completions", 20, 100, 120, null, null, null, 200, null),
            ("2026-04-17T10:00:00", "llama3.2", "/v1/chat/completions", 30, 150, 180, null, null, null, 200, null),
            ("2026-04-18T10:00:00", "llama3.2", "/v1/chat/completions", 40, 200, 240, null, null, null, 200, null),
            ("2026-04-19T10:00:00", "llama3.2", "/v1/chat/completions", 50, 250, 300, null, null, null, 200, null));
        var service = CreateService();

        var page1 = await service.GetRecentRequestsAsync(2, 0, null);
        Assert.Equal(5, page1.Total);
        Assert.Equal(2, page1.Items.Length);
        Assert.Equal(2, page1.Limit);
        Assert.Equal(0, page1.Offset);
        Assert.Equal("2026-04-19", page1.Items[0].Timestamp.Substring(0, 10));
        Assert.Equal("2026-04-18", page1.Items[1].Timestamp.Substring(0, 10));

        var page2 = await service.GetRecentRequestsAsync(2, 2, null);
        Assert.Equal(5, page2.Total);
        Assert.Equal(2, page2.Items.Length);
        Assert.Equal(2, page2.Offset);
        Assert.Equal("2026-04-17", page2.Items[0].Timestamp.Substring(0, 10));
        Assert.Equal("2026-04-16", page2.Items[1].Timestamp.Substring(0, 10));

        var page3 = await service.GetRecentRequestsAsync(2, 4, null);
        Assert.Single(page3.Items);
        Assert.Equal("2026-04-15", page3.Items[0].Timestamp.Substring(0, 10));
    }

    [Fact]
    public async Task GetRecentRequestsAsync_ModelFilter_ReturnsOnlyMatchingModel()
    {
        await SeedDataAsync(
            ("2026-04-15T10:00:00", "llama3.2", "/v1/chat/completions", 10, 50, 60, null, null, null, 200, null),
            ("2026-04-16T10:00:00", "mistral", "/v1/chat/completions", 20, 100, 120, null, null, null, 200, null),
            ("2026-04-17T10:00:00", "llama3.2", "/v1/chat/completions", 30, 150, 180, null, null, null, 200, null));
        var result = await CreateService().GetRecentRequestsAsync(10, 0, "llama3.2");
        Assert.Equal(2, result.Total);
        Assert.Equal(2, result.Items.Length);
        Assert.All(result.Items, r => Assert.Equal("llama3.2", r.Model));
    }



    [Fact]
    public async Task GetDailyUsageAsync_ModelFilter_ReturnsOnlyMatchingModel()
    {
        await SeedDataAsync(
            ("2026-04-15T10:00:00", "llama3.2", "/v1/chat/completions", 10, 50, 60, 100.0, 200.0, 0, 200, null),
            ("2026-04-15T11:00:00", "mistral", "/v1/chat/completions", 20, 100, 120, 150.0, 300.0, 0, 200, null),
            ("2026-04-15T12:00:00", "llama3.2", "/v1/chat/completions", 30, 150, 180, 200.0, 400.0, 0, 200, null));
        var service = CreateService();
        var result = await service.GetDailyUsageAsync(30, "llama3.2");
        Assert.All(result.Daily, r => Assert.Equal("llama3.2", r.Model));
        Assert.Single(result.Daily);
        Assert.Equal(2, result.Daily[0].Requests);
        Assert.Equal(40, result.Daily[0].PromptTokens);
        Assert.Equal(2, result.Summary.TotalRequests);
    }

    [Fact]
    public async Task GetDailyUsageAsync_CacheHitRate_CalculatedCorrectly()
    {
        await SeedDataAsync(
            ("2026-04-15T10:00:00", "llama3.2", "/v1/chat/completions", 10, 50, 60, 100.0, 200.0, 1, 200, null),
            ("2026-04-15T11:00:00", "llama3.2", "/v1/chat/completions", 20, 100, 120, 150.0, 300.0, 0, 200, null),
            ("2026-04-15T12:00:00", "llama3.2", "/v1/chat/completions", 30, 150, 180, 200.0, 400.0, 1, 200, null));
        var result = await CreateService().GetDailyUsageAsync(30, null);
        Assert.Equal(3, result.Daily[0].Requests);
        var expectedRate = 2.0 / 3.0;
        Assert.Equal(expectedRate, result.Daily[0].CacheHitRate, 5);
        Assert.Equal(expectedRate, result.Summary.CacheHitRate, 5);
    }

    [Fact]
    public async Task GetDailyUsageAsync_AllCacheHits_ReturnsRate1()
    {
        await SeedDataAsync(
            ("2026-04-15T10:00:00", "llama3.2", "/v1/chat/completions", 10, 50, 60, 100.0, 200.0, 5, 200, null),
            ("2026-04-15T11:00:00", "llama3.2", "/v1/chat/completions", 20, 100, 120, 150.0, 300.0, 3, 200, null));
        var result = await CreateService().GetDailyUsageAsync(30, null);
        Assert.Equal(1.0, result.Daily[0].CacheHitRate);
        Assert.Equal(1.0, result.Summary.CacheHitRate);
    }

    [Fact]
    public async Task GetRecentRequestsAsync_StoredFields()
    {
        await SeedDataAsync(
            ("2026-04-15T10:00:00", "llama3.2", "/v1/chat/completions", 10, 50, 60, 120.5, 450.3, 5, 200, "192.168.1.100"));
        var result = await CreateService().GetRecentRequestsAsync(10, 0, null);
        var r = result.Items[0];
        Assert.Equal(1, r.Id);
        Assert.Equal("llama3.2", r.Model);
        Assert.Equal("/v1/chat/completions", r.Route);
        Assert.Equal(10, r.PromptTokens);
        Assert.Equal(50, r.CompletionTokens);
        Assert.Equal(60, r.TotalTokens);
        Assert.Equal(120.5, r.PromptMs);
        Assert.Equal(450.3, r.CompletionMs);
        Assert.Equal(5, r.CacheHits);
        Assert.Equal(200, r.StatusCode);
        Assert.Equal("192.168.1.100", r.ClientIp);
    }

    [Fact]
    public async Task GetDailyUsageAsync_MultipleDaysMultipleModels_Comprehensive()
    {
        await SeedDataAsync(
            ("2026-04-15T10:00:00", "llama3.2", "/v1/chat/completions", 10, 50, 60, 100.0, 200.0, 1, 200, null),
            ("2026-04-15T11:00:00", "llama3.2", "/v1/chat/completions", 20, 100, 120, 150.0, 300.0, 0, 200, null),
            ("2026-04-15T12:00:00", "mistral", "/v1/chat/completions", 30, 150, 180, 200.0, 400.0, 1, 200, null),
            ("2026-04-16T10:00:00", "llama3.2", "/v1/chat/completions", 40, 200, 240, 250.0, 500.0, 0, 200, null),
            ("2026-04-16T11:00:00", "mistral", "/v1/chat/completions", 50, 250, 300, 300.0, 600.0, 0, 200, null));
        var result = await CreateService().GetDailyUsageAsync(30, null);

        Assert.Equal(4, result.Daily.Length);
        var may10llama = result.Daily.First(r => r.Date == "2026-04-15" && r.Model == "llama3.2");
        var may10mistral = result.Daily.First(r => r.Date == "2026-04-15" && r.Model == "mistral");
        var may11llama = result.Daily.First(r => r.Date == "2026-04-16" && r.Model == "llama3.2");
        var may11mistral = result.Daily.First(r => r.Date == "2026-04-16" && r.Model == "mistral");

        Assert.Equal(2, may10llama.Requests);
        Assert.Equal(30, may10llama.PromptTokens);
        Assert.Equal(150, may10llama.CompletionTokens);
        Assert.Equal(0.5, may10llama.CacheHitRate);
        Assert.Equal(1, may10mistral.Requests);
        Assert.Equal(1.0, may10mistral.CacheHitRate);
        Assert.Equal(1, may11llama.Requests);
        Assert.Equal(40, may11llama.PromptTokens);
        Assert.Equal(1, may11mistral.Requests);
        Assert.Equal(50, may11mistral.PromptTokens);

        Assert.Equal(5, result.Summary.TotalRequests);
        Assert.Equal(150, result.Summary.TotalPromptTokens);
        Assert.Equal(750, result.Summary.TotalCompletionTokens);
        Assert.Equal(900, result.Summary.TotalTokens);
    }

    public void Dispose()
    {
        // Database already exists from previous test; using fresh connection ensures clean state
    }
}
