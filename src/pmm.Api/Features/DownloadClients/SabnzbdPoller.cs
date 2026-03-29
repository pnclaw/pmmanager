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
    public async Task<List<SabnzbdPollResult>> PollAsync(
        DownloadClient client, IEnumerable<DownloadLog> logs, CancellationToken ct)
    {
        var results = new List<SabnzbdPollResult>();

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

    private async Task<List<SabnzbdPollResult>> PollQueueAsync(
        DownloadClient client, IEnumerable<string> nzoIds, CancellationToken ct)
    {
        var results = new List<SabnzbdPollResult>();
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

                results.Add(new SabnzbdPollResult
                {
                    ClientItemId   = nzoId,
                    Status         = status,
                    TotalSizeBytes = total,
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

    private async Task<SabnzbdPollResult?> PollHistoryAsync(
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

                var sabStatus = slot.TryGetProperty("status", out var stEl) ? stEl.GetString() : null;
                var status = MapHistoryStatus(sabStatus);

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

                List<string>? fileNames = null;
                if (status == DownloadStatus.Completed)
                    fileNames = ExtractFileNames(slot);

                return new SabnzbdPollResult
                {
                    ClientItemId    = nzoId,
                    Status          = status,
                    TotalSizeBytes  = totalBytes,
                    DownloadedBytes = status == DownloadStatus.Completed ? totalBytes : null,
                    StoragePath     = storagePath,
                    FileNames       = fileNames,
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

    /// <summary>
    /// Attempts to extract extracted filenames from the Unpack stage_log entry.
    /// Returns null if the information is unavailable.
    /// </summary>
    private static List<string>? ExtractFileNames(JsonElement slot)
    {
        if (!slot.TryGetProperty("stage_log", out var stageLog)) return null;

        foreach (var stage in stageLog.EnumerateArray())
        {
            var stageName = stage.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
            if (stageName is not ("Unpack" or "Moving")) continue;
            if (!stage.TryGetProperty("actions", out var actions)) continue;

            var files = new List<string>();
            foreach (var action in actions.EnumerateArray())
            {
                var text = action.GetString();
                if (text == null) continue;

                // e.g. "Unpacking movie.mkv" or "Unpacked movie.mkv from archive.rar"
                if (text.StartsWith("Unpacking ", StringComparison.OrdinalIgnoreCase))
                {
                    files.Add(text["Unpacking ".Length..].Trim());
                }
                else if (text.StartsWith("Unpacked ", StringComparison.OrdinalIgnoreCase))
                {
                    var part = text["Unpacked ".Length..].Trim();
                    var fromIdx = part.IndexOf(" from ", StringComparison.OrdinalIgnoreCase);
                    files.Add(fromIdx >= 0 ? part[..fromIdx].Trim() : part);
                }
            }

            if (files.Count > 0)
                return files.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
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

public class SabnzbdPollResult
{
    public string ClientItemId { get; init; } = string.Empty;
    public DownloadStatus Status { get; init; }
    public long? TotalSizeBytes { get; init; }
    public long? DownloadedBytes { get; init; }
    public string? StoragePath { get; init; }
    public List<string>? FileNames { get; init; }
    public string? ErrorMessage { get; init; }
}
