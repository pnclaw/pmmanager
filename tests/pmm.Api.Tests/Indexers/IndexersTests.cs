using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using pmm.Api.Features.Indexers;
using Pmm.Database.Enums;

namespace pmm.Api.Tests.Indexers;

public sealed class IndexersTests : IAsyncLifetime
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

    // ── GET /api/indexers ────────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_ReturnsOkWithArray()
    {
        var response = await _client.GetAsync("/api/indexers");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IndexerResponse[]>();
        body.Should().NotBeNull();
    }

    // ── POST /api/indexers ───────────────────────────────────────────────────

    [Fact]
    public async Task Create_WithValidRequest_Returns201WithBody()
    {
        var response = await _client.PostAsJsonAsync("/api/indexers", new
        {
            title = "NZBGeek",
            url = "https://api.nzbgeek.info",
            parsingType = (int)ParsingType.Newznab,
            isEnabled = true,
            apiKey = "abc123"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<IndexerResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().NotBeEmpty();
        body.Title.Should().Be("NZBGeek");
        body.Url.Should().Be("https://api.nzbgeek.info");
        body.ParsingType.Should().Be((int)ParsingType.Newznab);
        body.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task Create_WithEmptyTitle_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/indexers", new
        {
            title = "",
            url = "https://example.com",
            parsingType = (int)ParsingType.Newznab,
            isEnabled = true,
            apiKey = ""
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_WithEmptyUrl_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/indexers", new
        {
            title = "Test",
            url = "",
            parsingType = (int)ParsingType.Newznab,
            isEnabled = true,
            apiKey = ""
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── GET /api/indexers/{id} ───────────────────────────────────────────────

    [Fact]
    public async Task GetById_ExistingIndexer_Returns200WithCorrectData()
    {
        var created = await CreateIndexerAsync("GetById Test");

        var response = await _client.GetAsync($"/api/indexers/{created.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IndexerResponse>();
        body!.Id.Should().Be(created.Id);
        body.Title.Should().Be("GetById Test");
    }

    [Fact]
    public async Task GetById_NonExistentId_Returns404()
    {
        var response = await _client.GetAsync($"/api/indexers/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── PUT /api/indexers/{id} ───────────────────────────────────────────────

    [Fact]
    public async Task Update_ExistingIndexer_Returns200WithUpdatedData()
    {
        var created = await CreateIndexerAsync("Original Title");

        var response = await _client.PutAsJsonAsync($"/api/indexers/{created.Id}", new
        {
            title = "Updated Title",
            url = "https://updated.example.com",
            parsingType = (int)ParsingType.Newznab,
            isEnabled = false,
            apiKey = "new-key"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IndexerResponse>();
        body!.Title.Should().Be("Updated Title");
        body.Url.Should().Be("https://updated.example.com");
        body.IsEnabled.Should().BeFalse();
        body.ApiKey.Should().Be("new-key");
    }

    [Fact]
    public async Task Update_NonExistentId_Returns404()
    {
        var response = await _client.PutAsJsonAsync($"/api/indexers/{Guid.NewGuid()}", new
        {
            title = "Title",
            url = "https://example.com",
            parsingType = (int)ParsingType.Newznab,
            isEnabled = true,
            apiKey = ""
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── DELETE /api/indexers/{id} ────────────────────────────────────────────

    [Fact]
    public async Task Delete_ExistingIndexer_Returns204AndIsGone()
    {
        var created = await CreateIndexerAsync("To Delete");

        var deleteResponse = await _client.DeleteAsync($"/api/indexers/{created.Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await _client.GetAsync($"/api/indexers/{created.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_NonExistentId_Returns404()
    {
        var response = await _client.DeleteAsync($"/api/indexers/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<IndexerResponse> CreateIndexerAsync(string title = "Test Indexer")
    {
        var response = await _client.PostAsJsonAsync("/api/indexers", new
        {
            title,
            url = "https://example.com/api",
            parsingType = (int)ParsingType.Newznab,
            isEnabled = true,
            apiKey = "test-key"
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IndexerResponse>())!;
    }
}
