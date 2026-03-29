using Microsoft.EntityFrameworkCore;
using Pmm.Database;

namespace pmm.Api.Features.Indexers.Matching;

public class IndexerRowMatchDebugResult
{
    public int RowsChecked { get; init; }
    public List<IndexerRowDebugEntry> Rows { get; init; } = [];
}

public class IndexerRowDebugEntry
{
    public Guid RowId { get; init; }
    public string Title { get; init; } = string.Empty;

    /// <summary>Matched | AlreadyMatched | MultipleMatches | NoMatch</summary>
    public string MatchStatus { get; init; } = string.Empty;

    /// <summary>Prename title(s) that triggered the result.</summary>
    public List<string> CandidatePreNames { get; init; } = [];

    /// <summary>Video title for Matched and AlreadyMatched entries.</summary>
    public string? MatchedVideoTitle { get; init; }
}

public class IndexerRowMatchService(AppDbContext db, ILogger<IndexerRowMatchService> logger)
{
    private static readonly TimeSpan MatchWindow = TimeSpan.FromDays(7);

    public async Task RunAsync(CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow - MatchWindow;

        // All IndexerRows from the last 48 h that do not yet have a match attempt.
        var rows = await db.IndexerRows
            .Where(r => r.CreatedAt > cutoff)
            .Where(r => !db.IndexerRowMatches.Any(m => m.IndexerRowId == r.Id))
            .Select(r => new { r.Id, TitleLower = r.Title.ToLower() })
            .ToListAsync(ct);

        if (rows.Count == 0)
        {
            logger.LogInformation("IndexerRowMatchService: no new indexer rows to match");
            await UpdateLastRunAtAsync(ct);
            return;
        }

        logger.LogInformation("IndexerRowMatchService: checking {Count} indexer row(s) for prename matches", rows.Count);

        // Load every prename whose lower-cased title appears in the candidate set.
        var lowerTitles = rows.Select(r => r.TitleLower).ToList();

        var prenames = await db.PrdbVideoPreNames
            .Where(p => lowerTitles.Contains(p.Title.ToLower()))
            .ToListAsync(ct);

        // Group prenames by their lower-cased title for fast lookup.
        var prenamesByTitle = prenames
            .GroupBy(p => p.Title.ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.ToList());

        var now = DateTime.UtcNow;
        int matched = 0, skippedMultiple = 0;

        foreach (var row in rows)
        {
            if (!prenamesByTitle.TryGetValue(row.TitleLower, out var candidates))
                continue;

            if (candidates.Count > 1)
            {
                logger.LogWarning(
                    "IndexerRowMatchService: indexer row {RowId} title '{Title}' matched {Count} prenames — skipping",
                    row.Id, row.TitleLower, candidates.Count);
                skippedMultiple++;
                continue;
            }

            var prename = candidates[0];
            db.IndexerRowMatches.Add(new IndexerRowMatch
            {
                Id               = Guid.NewGuid(),
                IndexerRowId     = row.Id,
                PrdbVideoId      = prename.VideoId,
                MatchedPreNameId = prename.Id,
                MatchedTitle     = prename.Title,
                MatchedAtUtc     = now,
            });
            matched++;
        }

        if (matched > 0)
            await db.SaveChangesAsync(ct);

        await UpdateLastRunAtAsync(ct);

        logger.LogInformation(
            "IndexerRowMatchService: {Matched} matched, {SkippedMultiple} skipped (multiple candidates), {NoMatch} no match",
            matched, skippedMultiple, rows.Count - matched - skippedMultiple);
    }

    private async Task UpdateLastRunAtAsync(CancellationToken ct)
    {
        var settings = await db.AppSettings.FirstAsync(ct);
        settings.IndexerRowMatchLastRunAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    // ── Debug run ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Read-only diagnostic run. Searches all IndexerRows (no time window) whose titles
    /// contain every word in <paramref name="search"/> (case-insensitive). Reports what
    /// the normal match run would do, without writing anything to the database.
    /// </summary>
    public async Task<IndexerRowMatchDebugResult> RunDebugAsync(string search, CancellationToken ct)
    {
        var words = search.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (words.Length == 0)
        {
            logger.LogInformation("IndexerRowMatchService [debug]: empty search string — nothing to do");
            return new IndexerRowMatchDebugResult();
        }

        // Build query: each word must appear in the title (case-insensitive)
        var query = db.IndexerRows.AsQueryable();
        foreach (var word in words)
        {
            var w = word.ToLower();
            query = query.Where(r => r.Title.ToLower().Contains(w));
        }

        var rows = await query
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new { r.Id, r.Title })
            .ToListAsync(ct);

        logger.LogInformation(
            "IndexerRowMatchService [debug]: '{Search}' → {Count} row(s) found",
            search, rows.Count);

        if (rows.Count == 0)
            return new IndexerRowMatchDebugResult();

        // Load existing matches for these rows
        var rowIds   = rows.Select(r => r.Id).ToList();
        var existing = await db.IndexerRowMatches
            .Where(m => rowIds.Contains(m.IndexerRowId))
            .Include(m => m.Video)
            .ToDictionaryAsync(m => m.IndexerRowId, ct);

        // Load prenames whose lower title matches any candidate row title
        var lowerTitles = rows.Select(r => r.Title.ToLower()).Distinct().ToList();
        var prenames    = await db.PrdbVideoPreNames
            .Where(p => lowerTitles.Contains(p.Title.ToLower()))
            .Include(p => p.Video)
            .ToListAsync(ct);

        var prenamesByTitle = prenames
            .GroupBy(p => p.Title.ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.ToList());

        var entries = new List<IndexerRowDebugEntry>(rows.Count);

        foreach (var row in rows)
        {
            logger.LogInformation(
                "IndexerRowMatchService [debug]: checking '{Title}' ({RowId})",
                row.Title, row.Id);

            var titleLower = row.Title.ToLower();

            if (existing.TryGetValue(row.Id, out var match))
            {
                logger.LogInformation(
                    "IndexerRowMatchService [debug]: '{Title}' — already matched → '{VideoTitle}'",
                    row.Title, match.Video.Title);
                entries.Add(new IndexerRowDebugEntry
                {
                    RowId             = row.Id,
                    Title             = row.Title,
                    MatchStatus       = "AlreadyMatched",
                    CandidatePreNames = [match.MatchedTitle],
                    MatchedVideoTitle = match.Video.Title,
                });
                continue;
            }

            if (!prenamesByTitle.TryGetValue(titleLower, out var candidates))
            {
                logger.LogInformation(
                    "IndexerRowMatchService [debug]: '{Title}' — no prename match",
                    row.Title);
                entries.Add(new IndexerRowDebugEntry
                {
                    RowId       = row.Id,
                    Title       = row.Title,
                    MatchStatus = "NoMatch",
                });
                continue;
            }

            if (candidates.Count > 1)
            {
                logger.LogWarning(
                    "IndexerRowMatchService [debug]: '{Title}' — {Count} candidates: {Names}",
                    row.Title, candidates.Count,
                    string.Join(", ", candidates.Select(c => $"'{c.Title}'")));
                entries.Add(new IndexerRowDebugEntry
                {
                    RowId             = row.Id,
                    Title             = row.Title,
                    MatchStatus       = "MultipleMatches",
                    CandidatePreNames = candidates.Select(c => c.Title).ToList(),
                });
                continue;
            }

            var prename = candidates[0];
            logger.LogInformation(
                "IndexerRowMatchService [debug]: '{Title}' → prename '{PreName}' → video '{VideoTitle}'",
                row.Title, prename.Title, prename.Video.Title);
            entries.Add(new IndexerRowDebugEntry
            {
                RowId             = row.Id,
                Title             = row.Title,
                MatchStatus       = "Matched",
                CandidatePreNames = [prename.Title],
                MatchedVideoTitle = prename.Video.Title,
            });
        }

        return new IndexerRowMatchDebugResult
        {
            RowsChecked = rows.Count,
            Rows        = entries,
        };
    }
}
