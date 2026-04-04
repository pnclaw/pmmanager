using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Pmm.Database;
using Pmm.Database.Enums;

namespace pmm.Api.Features.Indexers.Scraping;

public class IndexerBackfillService(
    AppDbContext db,
    IHttpClientFactory httpClientFactory,
    ILogger<IndexerBackfillService> logger)
{
    private const int PageSize = 100;
    private const int PagesPerRun = 3;
    private const int Category = 6000;

    public async Task RunAsync(CancellationToken ct = default)
    {
        var settings = await db.AppSettings.FirstAsync(ct);
        var now = DateTime.UtcNow;

        if (settings.IndexerBackfillCompletedAtUtc != null)
        {
            logger.LogInformation("IndexerBackfillService: backfill already completed at {CompletedAt}", settings.IndexerBackfillCompletedAtUtc);
            return;
        }

        settings.IndexerBackfillLastRunAtUtc = now;

        var enabledIndexers = await db.Indexers
            .Where(i => i.IsEnabled)
            .OrderBy(i => i.CreatedAt)
            .ThenBy(i => i.Title)
            .ToListAsync(ct);

        if (enabledIndexers.Count == 0)
        {
            settings.IndexerBackfillStartedAtUtc ??= now;
            settings.IndexerBackfillCutoffUtc ??= now.AddDays(-settings.IndexerBackfillDays);
            MarkCompleted(settings, now);
            await db.SaveChangesAsync(ct);
            logger.LogInformation("IndexerBackfillService: no enabled indexers, marking backfill complete");
            return;
        }

        if (settings.IndexerBackfillStartedAtUtc is null || settings.IndexerBackfillCutoffUtc is null)
        {
            settings.IndexerBackfillStartedAtUtc = now;
            settings.IndexerBackfillCutoffUtc = now.AddDays(-settings.IndexerBackfillDays);
            settings.IndexerBackfillCurrentIndexerId = enabledIndexers[0].Id;
            settings.IndexerBackfillCurrentOffset = 0;
        }

        var cutoffUtc = settings.IndexerBackfillCutoffUtc.Value;
        var currentIndexer = ResolveCurrentIndexer(settings, enabledIndexers);
        var pagesRemaining = PagesPerRun;

        while (pagesRemaining > 0 && currentIndexer is not null)
        {
            var offset = settings.IndexerBackfillCurrentOffset ?? 0;
            var result = await FetchPageAsync(currentIndexer, offset, ct);
            db.IndexerApiRequests.Add(MakeSearchRequest(currentIndexer.Id, result.Success, result.StatusCode, result.ResponseTimeMs));

            if (!result.Success)
            {
                await db.SaveChangesAsync(ct);
                logger.LogWarning("IndexerBackfillService: stopping run after failed request for indexer {Title} at offset {Offset}", currentIndexer.Title, offset);
                return;
            }

            if (result.Items.Count == 0)
            {
                AdvanceIndexer(settings, enabledIndexers, currentIndexer.Id, now);
                pagesRemaining--;
                continue;
            }

            var existingNzbIds = await db.IndexerRows
                .Where(r => r.IndexerId == currentIndexer.Id)
                .Select(r => r.NzbId)
                .ToHashSetAsync(ct);

            var newRows = new List<IndexerRow>();
            var pageReachedCutoff = true;

            foreach (var item in result.Items)
            {
                var isWithinWindow = item.NzbPublishedAt is null || item.NzbPublishedAt >= cutoffUtc;
                if (isWithinWindow)
                    pageReachedCutoff = false;

                if (!isWithinWindow || string.IsNullOrEmpty(item.NzbId) || !existingNzbIds.Add(item.NzbId))
                    continue;

                newRows.Add(new IndexerRow
                {
                    Id = Guid.NewGuid(),
                    IndexerId = currentIndexer.Id,
                    Title = item.Title,
                    NzbId = item.NzbId,
                    NzbUrl = item.NzbUrl,
                    NzbSize = item.NzbSize,
                    NzbPublishedAt = item.NzbPublishedAt,
                    FileSize = item.FileSize,
                    Category = item.Category,
                    CreatedAt = now,
                    UpdatedAt = now,
                });
            }

            if (newRows.Count > 0)
                db.IndexerRows.AddRange(newRows);

            if (pageReachedCutoff)
            {
                logger.LogInformation(
                    "IndexerBackfillService: indexer {Title} reached cutoff {Cutoff} at offset {Offset}",
                    currentIndexer.Title, cutoffUtc, offset);
                AdvanceIndexer(settings, enabledIndexers, currentIndexer.Id, now);
            }
            else
            {
                settings.IndexerBackfillCurrentOffset = offset + PageSize;
            }

            await db.SaveChangesAsync(ct);

            if (newRows.Count > 0)
            {
                logger.LogInformation(
                    "IndexerBackfillService: saved {Count} new rows for indexer {Title} at offset {Offset}",
                    newRows.Count, currentIndexer.Title, offset);
            }

            pagesRemaining--;

            if (settings.IndexerBackfillCompletedAtUtc != null)
                return;

            currentIndexer = ResolveCurrentIndexer(settings, enabledIndexers);
        }
    }

    private async Task<IndexerPageFetchResult> FetchPageAsync(Indexer indexer, int offset, CancellationToken ct)
    {
        var baseUrl = $"{indexer.Url.TrimEnd('/')}{indexer.ApiPath}";
        var url = $"{baseUrl}?t=search&cat={Category}&apikey={indexer.ApiKey}&offset={offset}&limit={PageSize}";
        var client = httpClientFactory.CreateClient();
        var sw = Stopwatch.StartNew();

        try
        {
            var response = await client.GetAsync(url, ct);
            sw.Stop();
            var xml = await response.Content.ReadAsStringAsync(ct);

            return new IndexerPageFetchResult
            {
                Success = response.IsSuccessStatusCode,
                StatusCode = (int)response.StatusCode,
                ResponseTimeMs = (int)sw.ElapsedMilliseconds,
                Items = response.IsSuccessStatusCode ? NewznabParser.Parse(xml) : [],
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sw.Stop();
            logger.LogWarning(ex, "IndexerBackfillService: failed to fetch offset {Offset} for indexer {Title}", offset, indexer.Title);
            return new IndexerPageFetchResult
            {
                Success = false,
                StatusCode = null,
                ResponseTimeMs = (int)sw.ElapsedMilliseconds,
                Items = [],
            };
        }
    }

    private static Indexer? ResolveCurrentIndexer(AppSettings settings, List<Indexer> enabledIndexers)
    {
        if (enabledIndexers.Count == 0)
            return null;

        var currentIndexer = settings.IndexerBackfillCurrentIndexerId is null
            ? null
            : enabledIndexers.FirstOrDefault(i => i.Id == settings.IndexerBackfillCurrentIndexerId.Value);

        if (currentIndexer is not null)
            return currentIndexer;

        settings.IndexerBackfillCurrentIndexerId = enabledIndexers[0].Id;
        settings.IndexerBackfillCurrentOffset = 0;
        return enabledIndexers[0];
    }

    private static void AdvanceIndexer(AppSettings settings, List<Indexer> enabledIndexers, Guid currentIndexerId, DateTime now)
    {
        var currentIndex = enabledIndexers.FindIndex(i => i.Id == currentIndexerId);
        var nextIndexer = currentIndex >= 0 && currentIndex + 1 < enabledIndexers.Count
            ? enabledIndexers[currentIndex + 1]
            : null;

        if (nextIndexer is null)
        {
            MarkCompleted(settings, now);
            return;
        }

        settings.IndexerBackfillCurrentIndexerId = nextIndexer.Id;
        settings.IndexerBackfillCurrentOffset = 0;
    }

    private static void MarkCompleted(AppSettings settings, DateTime now)
    {
        settings.IndexerBackfillCompletedAtUtc = now;
        settings.IndexerBackfillCurrentIndexerId = null;
        settings.IndexerBackfillCurrentOffset = null;
    }

    private static IndexerApiRequest MakeSearchRequest(Guid indexerId, bool success, int? statusCode, int responseTimeMs) => new()
    {
        Id = Guid.NewGuid(),
        IndexerId = indexerId,
        RequestType = IndexerRequestType.Search,
        OccurredAt = DateTime.UtcNow,
        Success = success,
        HttpStatusCode = statusCode,
        ResponseTimeMs = responseTimeMs,
    };

    private sealed class IndexerPageFetchResult
    {
        public bool Success { get; init; }
        public int? StatusCode { get; init; }
        public int ResponseTimeMs { get; init; }
        public IReadOnlyList<ParsedIndexerRow> Items { get; init; } = [];
    }
}
