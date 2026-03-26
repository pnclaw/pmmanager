using Pmm.Database;

namespace pmm.Api.Background;

public class SyncWorker(IServiceScopeFactory scopeFactory, IHttpClientFactory httpClientFactory, ILogger<SyncWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        logger.LogInformation("SyncWorker started");

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await RunAsync(ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "SyncWorker encountered an error");
            }

            await Task.Delay(Interval, ct).ConfigureAwait(false);
        }

        logger.LogInformation("SyncWorker stopped");
    }

    private async Task RunAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var http = httpClientFactory.CreateClient();

        // TODO: implement sync logic
        await Task.CompletedTask;
    }
}
