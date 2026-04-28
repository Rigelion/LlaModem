using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace LlaModem.Tests;

public class ModelSwitchingFlowTests : IAsyncLifetime
{
    private TestAppHost? _host;

    public async Task InitializeAsync()
    {
        _host = new TestAppHost();
        await _host.InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        await _host!.DisposeAsync();
    }

    [Fact]
    public async Task FirstRequest_StartsSmartModel_And_Forwards_To_Backend()
    {
        // Arrange
        var requestPayload = new { model = "qwen-smart", messages = new[] { new { role = "user", content = "Hello" } } };
        var body = new StringContent(JsonSerializer.Serialize(requestPayload), Encoding.UTF8, "application/json");
        _host!.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes("testuser:testpass")));
        _host.Client.DefaultRequestHeaders.Add("X-Llama-Model", "qwen-smart");

        // Act
        var response = await _host.Client.PostAsync("/v1/chat/completions", body);

        // Assert — 200 OK (forwarded from mock backend)
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

        // Verify the smart backend received the request
        Assert.True(_host.SmartServer.RequestCount > 0, "Mock smart backend should have received a request");
        Assert.NotNull(_host.SmartServer.LastRequestBody);
        Assert.Contains("Hello", _host.SmartServer.LastRequestBody!);

        // Verify fast backend was never contacted
        Assert.True(_host.FastServer.RequestCount == 0, "Mock fast backend should not have been contacted yet");
    }

    [Fact]
    public async Task SecondRequestForDifferentModel_StopsSmart_And_StartsFast()
    {
        // Arrange: set auth
        _host!.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes("testuser:testpass")));

        // First request to qwen-smart
        var smartPayload = new { model = "qwen-smart", messages = new[] { new { role = "user", content = "First" } } };
        var smartBody = new StringContent(JsonSerializer.Serialize(smartPayload), Encoding.UTF8, "application/json");
        _host.Client.DefaultRequestHeaders.Add("X-Llama-Model", "qwen-smart");

        var firstResponse = await _host.Client.PostAsync("/v1/chat/completions", smartBody);
        Assert.Equal(System.Net.HttpStatusCode.OK, firstResponse.StatusCode);

        // Act: second request to qwen-fast — triggers model switch
        var fastPayload = new { model = "qwen-fast", messages = new[] { new { role = "user", content = "Second" } } };
        var fastBody = new StringContent(JsonSerializer.Serialize(fastPayload), Encoding.UTF8, "application/json");
        _host.Client.DefaultRequestHeaders.Add("X-Llama-Model", "qwen-fast");

        var secondResponse = await _host.Client.PostAsync("/v1/chat/completions", fastBody);

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.OK, secondResponse.StatusCode);

        // Both backends should have received a request
        Assert.True(_host.SmartServer.RequestCount >= 1, "Smart backend should have received at least one request");
        Assert.True(_host.FastServer.RequestCount >= 1, "Fast backend should have received at least one request");

        // The second request body should contain "Second" (fast model's payload)
        Assert.Contains("Second", _host.FastServer.LastRequestBody!);
    }

    [Fact]
    public async Task MissingModelHeader_Returns400()
    {
        // Arrange — no X-Llama-Model header
        _host!.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes("testuser:testpass")));
        var body = new StringContent("{}");

        // Act
        var response = await _host.Client.PostAsync("/v1/chat/completions", body);

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.Equal("Missing header", error?["error"]);
    }

    [Fact]
    public async Task UnknownModel_Returns400()
    {
        // Arrange
        _host!.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes("testuser:testpass")));
        _host.Client.DefaultRequestHeaders.Add("X-Llama-Model", "qwen-turbo"); // not in config
        var body = new StringContent("{}");

        // Act
        var response = await _host.Client.PostAsync("/v1/chat/completions", body);

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.Equal("Unknown model", error?["error"]);
    }

    [Fact]
    public async Task MissingAuth_Returns401()
    {
        // Arrange — no auth header
        var unauthClient = _host!.Client;
        var body = new StringContent("{}");

        // Act
        var response = await unauthClient.PostAsync("/v1/chat/completions", body);

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("Basic", response.Headers.WwwAuthenticate.ToString());
    }

    [Fact]
    public async Task HealthEndpoint_Returns200_WithActiveModel()
    {
        // Arrange: trigger a model start
        _host!.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes("testuser:testpass")));
        _host.Client.DefaultRequestHeaders.Add("X-Llama-Model", "qwen-smart");
        var payload = new { model = "qwen-smart", messages = Array.Empty<object>() };
        var body = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        await _host.Client.PostAsync("/v1/chat/completions", body);

        // Act
        var response = await _host.Client.GetAsync("/health");

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        var data = await response.Content.ReadFromJsonAsync<Dictionary<string, object?>>();
        Assert.Equal("ok", data?["status"]);
        Assert.Equal("qwen-smart", data?["activeModel"]);
    }

    [Fact]
    public async Task AdminStatus_ReturnsActiveModelInfo()
    {
        // Arrange: trigger a model start
        _host!.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes("testuser:testpass")));
        _host.Client.DefaultRequestHeaders.Add("X-Llama-Model", "qwen-fast");
        var payload = new { model = "qwen-fast", messages = Array.Empty<object>() };
        var body = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        await _host.Client.PostAsync("/v1/chat/completions", body);

        // Act
        var response = await _host.Client.GetAsync("/admin/status");

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        var data = await response.Content.ReadFromJsonAsync<Dictionary<string, string?>>();
        Assert.Equal("qwen-fast", data?["activeModel"]);
        Assert.NotNull(data?["backendUrl"]);
    }

    [Fact]
    public async Task AdminStop_StopsActiveModel()
    {
        // Arrange: trigger a model start
        _host!.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes("testuser:testpass")));
        _host.Client.DefaultRequestHeaders.Add("X-Llama-Model", "qwen-smart");
        var payload = new { model = "qwen-smart", messages = Array.Empty<object>() };
        var body = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        await _host.Client.PostAsync("/v1/chat/completions", body);

        // Act
        var response = await _host.Client.PostAsync("/admin/stop", null);

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

        // Verify model is stopped via health endpoint
        var healthResponse = await _host.Client.GetAsync("/health");
        var healthData = await healthResponse.Content.ReadFromJsonAsync<Dictionary<string, object?>>();
        Assert.Null(healthData?["activeModel"]);
    }

    [Fact]
    public async Task AdminSwitchModel_ChangesActiveModel()
    {
        // Arrange: start smart model
        _host!.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes("testuser:testpass")));
        _host.Client.DefaultRequestHeaders.Add("X-Llama-Model", "qwen-smart");
        var smartPayload = new { model = "qwen-smart", messages = Array.Empty<object>() };
        var smartBody = new StringContent(JsonSerializer.Serialize(smartPayload), Encoding.UTF8, "application/json");
        await _host.Client.PostAsync("/v1/chat/completions", smartBody);

        // Act: switch to fast via admin endpoint
        var switchPayload = new { model = "qwen-fast" };
        var switchBody = new StringContent(JsonSerializer.Serialize(switchPayload), Encoding.UTF8, "application/json");
        var switchResponse = await _host.Client.PostAsync("/admin/model", switchBody);

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.OK, switchResponse.StatusCode);
        var switchData = await switchResponse.Content.ReadFromJsonAsync<Dictionary<string, string?>>();
        Assert.Equal("qwen-fast", switchData?["activeModel"]);

        // Verify via health
        var healthResponse = await _host.Client.GetAsync("/health");
        var healthData = await healthResponse.Content.ReadFromJsonAsync<Dictionary<string, object?>>();
        Assert.Equal("qwen-fast", healthData?["activeModel"]);
    }

    [Fact]
    public async Task Proxy_Forwards_Path_Correctly()
    {
        // Arrange: ensure smart model is running
        _host!.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes("testuser:testpass")));
        _host.Client.DefaultRequestHeaders.Add("X-Llama-Model", "qwen-smart");
        var payload = new { model = "qwen-smart", messages = Array.Empty<object>() };
        var body = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        await _host.Client.PostAsync("/v1/chat/completions", body);

        // Act: send request to /v1/models (another OpenAI route)
        var modelsResponse = await _host.Client.GetAsync("/v1/models");

        // Assert — should be forwarded to mock backend (200 OK)
        Assert.Equal(System.Net.HttpStatusCode.OK, modelsResponse.StatusCode);
    }
}
