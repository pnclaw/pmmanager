using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using pmm.Api.Features.Settings;
using Pmm.Database;
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
        body.IndexerBackfillDays.Should().Be(30);
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
            indexerBackfillDays = 45,
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SettingsResponse>();
        body.Should().NotBeNull();
        body!.PrdbApiKey.Should().Be("my-secret-key");
        body.PrdbApiUrl.Should().Be("https://custom.prdb.net");
        body.PreferredVideoQuality.Should().Be((int)VideoQuality.P1080);
        body.IndexerBackfillDays.Should().Be(45);
    }

    [Fact]
    public async Task Update_PersistsChanges()
    {
        await _client.PutAsJsonAsync("/api/settings", new
        {
            prdbApiKey = "persisted-key",
            prdbApiUrl = "https://api.prdb.net",
            preferredVideoQuality = (int)VideoQuality.P720,
            indexerBackfillDays = 14,
        });

        var response = await _client.GetAsync("/api/settings");
        var body = await response.Content.ReadFromJsonAsync<SettingsResponse>();
        body!.PrdbApiKey.Should().Be("persisted-key");
        body.PreferredVideoQuality.Should().Be((int)VideoQuality.P720);
        body.IndexerBackfillDays.Should().Be(14);
    }

    [Fact]
    public async Task Update_IncreasingIndexerBackfillDays_ResetsIndexerBackfillState()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var settings = await db.AppSettings.FirstAsync();
            settings.IndexerBackfillDays = 30;
            settings.IndexerBackfillStartedAtUtc = DateTime.UtcNow.AddHours(-2);
            settings.IndexerBackfillCutoffUtc = DateTime.UtcNow.AddDays(-30);
            settings.IndexerBackfillCompletedAtUtc = DateTime.UtcNow.AddHours(-1);
            settings.IndexerBackfillLastRunAtUtc = DateTime.UtcNow.AddMinutes(-15);
            settings.IndexerBackfillCurrentIndexerId = Guid.NewGuid();
            settings.IndexerBackfillCurrentOffset = 500;
            await db.SaveChangesAsync();
        }

        var response = await _client.PutAsJsonAsync("/api/settings", new
        {
            prdbApiKey = "",
            prdbApiUrl = "https://api.prdb.net",
            preferredVideoQuality = (int)VideoQuality.P2160,
            safeForWork = false,
            indexerBackfillDays = 60,
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var updated = await verifyDb.AppSettings.FirstAsync();
        updated.IndexerBackfillDays.Should().Be(60);
        updated.IndexerBackfillStartedAtUtc.Should().BeNull();
        updated.IndexerBackfillCutoffUtc.Should().BeNull();
        updated.IndexerBackfillCompletedAtUtc.Should().BeNull();
        updated.IndexerBackfillLastRunAtUtc.Should().BeNull();
        updated.IndexerBackfillCurrentIndexerId.Should().BeNull();
        updated.IndexerBackfillCurrentOffset.Should().BeNull();
    }

    [Fact]
    public async Task Update_DecreasingIndexerBackfillDays_DoesNotResetIndexerBackfillState()
    {
        var startedAt = DateTime.UtcNow.AddHours(-2);
        var cutoffUtc = DateTime.UtcNow.AddDays(-30);
        var completedAt = DateTime.UtcNow.AddHours(-1);
        var lastRunAt = DateTime.UtcNow.AddMinutes(-15);
        var currentIndexerId = Guid.NewGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var settings = await db.AppSettings.FirstAsync();
            settings.IndexerBackfillDays = 30;
            settings.IndexerBackfillStartedAtUtc = startedAt;
            settings.IndexerBackfillCutoffUtc = cutoffUtc;
            settings.IndexerBackfillCompletedAtUtc = completedAt;
            settings.IndexerBackfillLastRunAtUtc = lastRunAt;
            settings.IndexerBackfillCurrentIndexerId = currentIndexerId;
            settings.IndexerBackfillCurrentOffset = 500;
            await db.SaveChangesAsync();
        }

        var response = await _client.PutAsJsonAsync("/api/settings", new
        {
            prdbApiKey = "",
            prdbApiUrl = "https://api.prdb.net",
            preferredVideoQuality = (int)VideoQuality.P2160,
            safeForWork = false,
            indexerBackfillDays = 14,
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var updated = await verifyDb.AppSettings.FirstAsync();
        updated.IndexerBackfillDays.Should().Be(14);
        updated.IndexerBackfillStartedAtUtc.Should().Be(startedAt);
        updated.IndexerBackfillCutoffUtc.Should().Be(cutoffUtc);
        updated.IndexerBackfillCompletedAtUtc.Should().Be(completedAt);
        updated.IndexerBackfillLastRunAtUtc.Should().Be(lastRunAt);
        updated.IndexerBackfillCurrentIndexerId.Should().Be(currentIndexerId);
        updated.IndexerBackfillCurrentOffset.Should().Be(500);
    }
}
