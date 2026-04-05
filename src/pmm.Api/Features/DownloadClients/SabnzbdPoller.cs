using System.Text.Json;
using Pmm.Database;
using Pmm.Database.Enums;

namespace pmm.Api.Features.DownloadClients;

public class SabnzbdPoller(IHttpClientFactory httpClientFactory, ILogger<SabnzbdPoller> logger)
{
    /// <summary>
    /// Polls the SABnzbd queue and history for the supplied logs and returns updated snapshots.
    /// Items found in the queue are in progress; items not in the queue are looked up in history.
    /// </summary>
    public async Task<List<DownloadPollResult>> PollAsync(
        DownloadClient client, IEnumerable<DownloadLog> logs, CancellationToken ct)
    {
        var results = new List<DownloadPollResult>();

        var pendingById = logs
            .Where(l => l.ClientItemId != null)
            .ToDictionary(l => l.ClientItemId!);

        if (pendingById.Count == 0) return results;

        var queueHits = await PollQueueAsync(client, pendingById.Keys, ct);

        foreach (var hit in queueHits)
        {
            results.Add(hit);
            pendingById.Remove(hit.ClientItemId);
        }

        // Items not in queue — check history
        foreach (var (nzoId, _) in pendingById)
        {
            var historyResult = await PollHistoryAsync(client, nzoId, ct);
            if (historyResult != null)
                results.Add(historyResult);
        }

        return results;
    }

    private async Task<List<DownloadPollResult>> PollQueueAsync(
        DownloadClient client, IEnumerable<string> nzoIds, CancellationToken ct)
    {
        var results = new List<DownloadPollResult>();
        var scheme = client.UseSsl ? "https" : "http";
        var url = $"{scheme}://{client.Host}:{client.Port}/api?mode=queue&output=json&apikey={client.ApiKey}";

        try
        {
            var http = CreateClient();
            var json = await http.GetStringAsync(url, ct);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("queue", out var queue)) return results;
            if (!queue.TryGetProperty("slots", out var slots)) return results;

            var wanted = new HashSet<string>(nzoIds);

            foreach (var slot in slots.EnumerateArray())
            {
                var nzoId = slot.TryGetProperty("nzo_id", out var nzoEl) ? nzoEl.GetString() : null;
                if (nzoId == null || !wanted.Contains(nzoId)) continue;

                var status = MapQueueStatus(
                    slot.TryGetProperty("status", out var stEl) ? stEl.GetString() : null);

                long? total = null;
                long? downloaded = null;
                if (slot.TryGetProperty("mb", out var mbEl) &&
                    double.TryParse(mbEl.GetString(), System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var totalMb))
                {
                    total = (long)(totalMb * 1024 * 1024);
                }
                if (total.HasValue &&
                    slot.TryGetProperty("mbleft", out var mbLeftEl) &&
                    double.TryParse(mbLeftEl.GetString(), System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var leftMb))
                {
                    downloaded = total.Value - (long)(leftMb * 1024 * 1024);
                }

                results.Add(new DownloadPollResult
                {
                    ClientItemId    = nzoId,
                    Status          = status,
                    TotalSizeBytes  = total,
                    DownloadedBytes = downloaded,
                });
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to poll SABnzbd queue for client {ClientId}", client.Id);
        }

        return results;
    }

    private async Task<DownloadPollResult?> PollHistoryAsync(
        DownloadClient client, string nzoId, CancellationToken ct)
    {
        var scheme = client.UseSsl ? "https" : "http";
        var url = $"{scheme}://{client.Host}:{client.Port}/api" +
                  $"?mode=history&output=json&apikey={client.ApiKey}&nzo_id={nzoId}";

        try
        {
            var http = CreateClient();
            var json = await http.GetStringAsync(url, ct);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("history", out var history)) return null;
            if (!history.TryGetProperty("slots", out var slots)) return null;

            foreach (var slot in slots.EnumerateArray())
            {
                var id = slot.TryGetProperty("nzo_id", out var idEl) ? idEl.GetString() : null;
                if (id != nzoId) continue;

                var status = MapHistoryStatus(
                    slot.TryGetProperty("status", out var stEl) ? stEl.GetString() : null);

                long? totalBytes = slot.TryGetProperty("bytes", out var bytesEl)
                    ? bytesEl.GetInt64()
                    : null;

                string? storagePath = slot.TryGetProperty("storage", out var storEl)
                    ? storEl.GetString()
                    : null;

                string? errorMessage = null;
                if (status == DownloadStatus.Failed &&
                    slot.TryGetProperty("fail_message", out var failEl))
                {
                    var msg = failEl.GetString();
                    if (!string.IsNullOrWhiteSpace(msg))
                        errorMessage = msg;
                }

                return new DownloadPollResult
                {
                    ClientItemId    = nzoId,
                    Status          = status,
                    TotalSizeBytes  = totalBytes,
                    DownloadedBytes = status == DownloadStatus.Completed ? totalBytes : null,
                    StoragePath     = storagePath,
                    ErrorMessage    = errorMessage,
                };
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to poll SABnzbd history for nzo_id {NzoId}", nzoId);
        }

        return null;
    }

    private static DownloadStatus MapQueueStatus(string? status) => status?.ToUpperInvariant() switch
    {
        "DOWNLOADING" or "FETCHING" or "GRABBING" => DownloadStatus.Downloading,
        "VERIFYING" or "REPAIRING" or "EXTRACTING" or "MOVING" or "RUNNING" => DownloadStatus.PostProcessing,
        _ => DownloadStatus.Queued,
    };

    private static DownloadStatus MapHistoryStatus(string? status) => status?.ToUpperInvariant() switch
    {
        "COMPLETED" => DownloadStatus.Completed,
        "FAILED"    => DownloadStatus.Failed,
        _           => DownloadStatus.PostProcessing,
    };

    private HttpClient CreateClient()
    {
        var http = httpClientFactory.CreateClient();
        http.Timeout = TimeSpan.FromSeconds(10);
        return http;
    }
}
