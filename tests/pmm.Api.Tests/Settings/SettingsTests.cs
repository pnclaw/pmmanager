using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using pmm.Api.Features.Settings;
using Pmm.Database.Enums;

namespace pmm.Api.Tests.Settings;

public sealed class SettingsTests : IAsyncLifetime
{
    private readonly PmmApiFactory _factory = new();
    private HttpClient _client = null!;

    public Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    // ── GET /api/settings ────────────────────────────────────────────────────

    [Fact]
    public async Task Get_ReturnsOkWithDefaults()
    {
        var response = await _client.GetAsync("/api/settings");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SettingsResponse>();
        body.Should().NotBeNull();
        body!.PrdbApiKey.Should().BeEmpty();
        body.PrdbApiUrl.Should().Be("https://api.prdb.net");
        body.PreferredVideoQuality.Should().Be((int)VideoQuality.P2160);
    }

    // ── PUT /api/settings ────────────────────────────────────────────────────

    [Fact]
    public async Task Update_WithValidRequest_ReturnsOkWithUpdatedValues()
    {
        var response = await _client.PutAsJsonAsync("/api/settings", new
        {
            prdbApiKey = "my-secret-key",
            prdbApiUrl = "https://custom.prdb.net",
            preferredVideoQuality = (int)VideoQuality.P1080,
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SettingsResponse>();
        body.Should().NotBeNull();
        body!.PrdbApiKey.Should().Be("my-secret-key");
        body.PrdbApiUrl.Should().Be("https://custom.prdb.net");
        body.PreferredVideoQuality.Should().Be((int)VideoQuality.P1080);
    }

    [Fact]
    public async Task Update_PersistsChanges()
    {
        await _client.PutAsJsonAsync("/api/settings", new
        {
            prdbApiKey = "persisted-key",
            prdbApiUrl = "https://api.prdb.net",
            preferredVideoQuality = (int)VideoQuality.P720,
        });

        var response = await _client.GetAsync("/api/settings");
        var body = await response.Content.ReadFromJsonAsync<SettingsResponse>();
        body!.PrdbApiKey.Should().Be("persisted-key");
        body.PreferredVideoQuality.Should().Be((int)VideoQuality.P720);
    }
}
