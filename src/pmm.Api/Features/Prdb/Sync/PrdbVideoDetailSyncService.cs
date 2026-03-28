using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pmm.Database;

namespace pmm.Api.Features.Prdb.Sync;

public class PrdbVideoDetailSyncService(
    AppDbContext db,
    IHttpClientFactory httpClientFactory,
    ILogger<PrdbVideoDetailSyncService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private const int VideosPerRun       = 200; // 200 API requests per run
    private const int ActorBatchSize     = 50;
    private const int ActorBatchesPerRun = 20;  // 1 000 actors per run, 20 API requests

    public async Task RunAsync(CancellationToken ct = default)
    {
        var settings = await db.AppSettings.FirstAsync(ct);

        if (string.IsNullOrWhiteSpace(settings.PrdbApiKey))
        {
            logger.LogWarning("PrdbVideoDetailSyncService: PrdbApiKey not configured — skipping");
            return;
        }

        var http = httpClientFactory.CreateClient();
        http.BaseAddress = new Uri(settings.PrdbApiUrl.TrimEnd('/') + "/");
        http.DefaultRequestHeaders.Add("X-Api-Key", settings.PrdbApiKey);

        await SyncVideoDetailsAsync(http, ct);
        await SyncActorDetailsAsync(http, ct);
    }

    // ── Phase 1: Video detail sync ───────────────────────────────────────────

    private async Task SyncVideoDetailsAsync(HttpClient http, CancellationToken ct)
    {
        var totalPending = await db.PrdbVideos.CountAsync(v => v.DetailSyncedAtUtc == null, ct);

        if (totalPending == 0)
        {
            logger.LogInformation("PrdbVideoDetailSyncService: no videos pending detail sync");
            return;
        }

        var videoIds = await db.PrdbVideos
            .Where(v => v.DetailSyncedAtUtc == null)
            .OrderBy(v => v.SyncedAtUtc)
            .Select(v => v.Id)
            .Take(VideosPerRun)
            .ToListAsync(ct);

        logger.LogInformation(
            "PrdbVideoDetailSyncService: syncing details for {Count} videos this run ({Pending} total pending)",
            videoIds.Count, totalPending);

        var existingActorIds = await db.PrdbActors
            .Select(a => a.Id)
            .ToHashSetAsync(ct);

        var synced = 0;

        foreach (var videoId in videoIds)
        {
            ct.ThrowIfCancellationRequested();

            var detail = await http.GetFromJsonAsync<PrdbApiVideoDetail>(
                $"videos/{videoId}", JsonOptions, ct);

            if (detail is null)
            {
                logger.LogWarning("PrdbVideoDetailSyncService: no detail returned for video {VideoId}", videoId);
                continue;
            }

            var now = DateTime.UtcNow;

            // Upsert images
            var existingImageIds = await db.PrdbVideoImages
                .Where(i => i.VideoId == videoId)
                .Select(i => i.Id)
                .ToHashSetAsync(ct);

            foreach (var img in detail.Images.Where(i => !existingImageIds.Contains(i.Id)))
            {
                db.PrdbVideoImages.Add(new PrdbVideoImage
                {
                    Id      = img.Id,
                    CdnPath = img.CdnPath,
                    VideoId = videoId,
                });
            }

            // Upsert pre-names
            var existingPreNameIds = await db.PrdbVideoPreNames
                .Where(p => p.VideoId == videoId)
                .Select(p => p.Id)
                .ToHashSetAsync(ct);

            foreach (var preName in detail.PreNames.Where(p => !existingPreNameIds.Contains(p.Id)))
            {
                db.PrdbVideoPreNames.Add(new PrdbVideoPreName
                {
                    Id      = preName.Id,
                    Title   = preName.Title,
                    VideoId = videoId,
                });
            }

            // Upsert VideoActor join entries; insert actor stubs for unknown actors
            var existingVideoActorIds = await db.PrdbVideoActors
                .Where(va => va.VideoId == videoId)
                .Select(va => va.ActorId)
                .ToHashSetAsync(ct);

            foreach (var actor in detail.Actors)
            {
                if (!existingVideoActorIds.Contains(actor.Id))
                {
                    db.PrdbVideoActors.Add(new PrdbVideoActor
                    {
                        VideoId = videoId,
                        ActorId = actor.Id,
                    });
                }

                if (!existingActorIds.Contains(actor.Id))
                {
                    db.PrdbActors.Add(new PrdbActor
                    {
                        Id               = actor.Id,
                        Name             = actor.Name,
                        Gender           = actor.Gender,
                        Birthday         = actor.Birthday,
                        Nationality      = actor.Nationality,
                        PrdbCreatedAtUtc = now,
                        PrdbUpdatedAtUtc = now,
                        SyncedAtUtc      = now,
                    });
                    existingActorIds.Add(actor.Id);
                }
            }

            // Mark video detail as synced
            var video = await db.PrdbVideos.FindAsync([videoId], ct);
            if (video is not null)
                video.DetailSyncedAtUtc = now;

            await db.SaveChangesAsync(ct);
            synced++;
        }

        logger.LogInformation("PrdbVideoDetailSyncService: synced details for {Count} videos", synced);
    }

    // ── Phase 2: Actor detail batch sync ─────────────────────────────────────

    private async Task SyncActorDetailsAsync(HttpClient http, CancellationToken ct)
    {
        var limit = ActorBatchSize * ActorBatchesPerRun;

        var actorIds = await db.PrdbActors
            .Where(a => a.DetailSyncedAtUtc == null)
            .OrderBy(a => a.SyncedAtUtc)
            .Select(a => a.Id)
            .Take(limit)
            .ToListAsync(ct);

        if (actorIds.Count == 0)
        {
            logger.LogInformation("PrdbVideoDetailSyncService: no actors pending detail sync");
            return;
        }

        var totalPending = await db.PrdbActors.CountAsync(a => a.DetailSyncedAtUtc == null, ct);
        logger.LogInformation(
            "PrdbVideoDetailSyncService: syncing details for {Count} actors this run ({Pending} total pending), batches of {BatchSize}",
            actorIds.Count, totalPending, ActorBatchSize);

        var synced = 0;

        foreach (var batch in actorIds.Chunk(ActorBatchSize))
        {
            ct.ThrowIfCancellationRequested();

            var request  = new PrdbApiBatchActorsRequest(batch.ToList());
            var response = await http.PostAsJsonAsync("actors/batch", request, ct);
            response.EnsureSuccessStatusCode();

            var details = await response.Content.ReadFromJsonAsync<List<PrdbApiActorDetail>>(JsonOptions, ct);
            if (details is null) continue;

            var now    = DateTime.UtcNow;
            var ids    = batch.ToHashSet();
            var actors = await db.PrdbActors
                .Include(a => a.Aliases)
                .Include(a => a.Images)
                .Where(a => ids.Contains(a.Id))
                .ToListAsync(ct);

            var actorMap = actors.ToDictionary(a => a.Id);

            foreach (var detail in details)
            {
                if (!actorMap.TryGetValue(detail.Id, out var actor)) continue;

                actor.Name             = detail.Name;
                actor.Gender           = detail.Gender;
                actor.Birthday         = detail.Birthday;
                actor.BirthdayType     = detail.BirthdayType;
                actor.Deathday         = detail.Deathday;
                actor.Birthplace       = detail.Birthplace;
                actor.Haircolor        = detail.Haircolor;
                actor.Eyecolor         = detail.Eyecolor;
                actor.BreastType       = detail.BreastType;
                actor.Height           = detail.Height;
                actor.BraSize          = detail.BraSize;
                actor.BraSizeLabel     = detail.BraSizeLabel;
                actor.WaistSize        = detail.WaistSize;
                actor.HipSize          = detail.HipSize;
                actor.Nationality      = detail.Nationality;
                actor.Ethnicity        = detail.Ethnicity;
                actor.CareerStart      = detail.CareerStart;
                actor.CareerEnd        = detail.CareerEnd;
                actor.Tattoos          = detail.Tattoos;
                actor.Piercings        = detail.Piercings;
                actor.PrdbUpdatedAtUtc = detail.UpdatedAtUtc;
                actor.SyncedAtUtc      = now;
                actor.DetailSyncedAtUtc = now;

                // Upsert aliases
                var existingAliasNames = actor.Aliases.Select(a => a.Name).ToHashSet();
                foreach (var alias in detail.Aliases.Where(a => !existingAliasNames.Contains(a.Name)))
                {
                    actor.Aliases.Add(new PrdbActorAlias { Name = alias.Name, SiteId = alias.SiteId });
                }

                // Upsert images
                var existingImageIds = actor.Images.Select(i => i.Id).ToHashSet();
                foreach (var img in detail.Images.Where(i => !existingImageIds.Contains(i.Id)))
                {
                    actor.Images.Add(new PrdbActorImage
                    {
                        Id        = img.Id,
                        ImageType = img.ImageType,
                        Url       = img.Url,
                        ActorId   = actor.Id,
                    });
                }

                synced++;
            }

            // Mark any actor in the batch that the API silently omitted (doesn't exist)
            foreach (var missingId in ids.Except(details.Select(d => d.Id)))
            {
                if (actorMap.TryGetValue(missingId, out var actor))
                    actor.DetailSyncedAtUtc = now;
            }

            await db.SaveChangesAsync(ct);
        }

        logger.LogInformation("PrdbVideoDetailSyncService: synced details for {Count} actors", synced);
    }
}
