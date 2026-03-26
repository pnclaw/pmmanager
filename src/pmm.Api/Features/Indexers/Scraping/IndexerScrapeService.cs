using Microsoft.EntityFrameworkCore;
using Pmm.Database;

namespace pmm.Api.Features.Indexers.Scraping;

public class IndexerScrapeService(AppDbContext db, IHttpClientFactory httpClientFactory, ILogger<IndexerScrapeService> logger)
{
    private const int PageSize = 100;
    private const int DefaultPages = 3;
    private const int Category = 6000;

    public async Task<int> ScrapeIndexerAsync(Indexer indexer, int pages = DefaultPages, CancellationToken ct = default)
    {
        var baseUrl = $"{indexer.Url.TrimEnd('/')}{indexer.ApiPath}";

        var existingNzbIds = await db.IndexerRows
            .Where(r => r.IndexerId == indexer.Id)
            .Select(r => r.NzbId)
            .ToHashSetAsync(ct);

        var newRows = new List<IndexerRow>();
        var client = httpClientFactory.CreateClient();

        for (var page = 0; page < pages; page++)
        {
            var offset = page * PageSize;
            var url = $"{baseUrl}?t=search&cat={Category}&apikey={indexer.ApiKey}&offset={offset}&limit={PageSize}";

            string xml;
            try
            {
                xml = await client.GetStringAsync(url, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to fetch page {Page} for indexer {Title}", page, indexer.Title);
                break;
            }

            var items = NewznabParser.Parse(xml);
            if (items.Count == 0) break;

            foreach (var item in items)
            {
                if (string.IsNullOrEmpty(item.NzbId) || !existingNzbIds.Add(item.NzbId))
                    continue;

                newRows.Add(new IndexerRow
                {
                    Id = Guid.NewGuid(),
                    IndexerId = indexer.Id,
                    Title = item.Title,
                    NzbId = item.NzbId,
                    NzbUrl = item.NzbUrl,
                    NzbSize = item.NzbSize,
                    NzbPublishedAt = item.NzbPublishedAt,
                    FileSize = item.FileSize,
                    Category = item.Category,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                });
            }
        }

        if (newRows.Count > 0)
        {
            db.IndexerRows.AddRange(newRows);
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Saved {Count} new rows for indexer {Title}", newRows.Count, indexer.Title);
        }

        return newRows.Count;
    }

    public async Task ScrapeAllEnabledAsync(CancellationToken ct = default)
    {
        var indexers = await db.Indexers
            .Where(i => i.IsEnabled)
            .ToListAsync(ct);

        foreach (var indexer in indexers)
        {
            if (ct.IsCancellationRequested) break;
            await ScrapeIndexerAsync(indexer, ct: ct);
        }
    }
}
