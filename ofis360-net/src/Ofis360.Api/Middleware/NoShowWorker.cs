using Ofis360.Application.Services;

namespace Ofis360.Api.Middleware;

/// <summary>Dakikada bir no-show islemini calistirir. Veritabani gecici kesilirse is durmaz, sonraki turda devam eder.</summary>
public sealed class NoShowWorker(IServiceScopeFactory scopes, ILogger<NoShowWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var result = await scope.ServiceProvider.GetRequiredService<NoShowService>().RunAsync(stoppingToken);
                if (result.FeedStatus == AttendanceFeedStatus.Unavailable)
                {
                    logger.LogWarning("Turnike verisi alinamadi; bu turda kimse gelmedi sayilmadi.");
                }
                if (result.HasChanges) logger.LogInformation("No-show islemi: {@Result}", result);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "No-show islemi basarisiz; sonraki turda yeniden denenecek.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }
}
