using CouponManager.Application;

namespace CouponManager.Api;

public sealed class InactiveCouponCleanupService(
    IServiceScopeFactory scopeFactory,
    IWebHostEnvironment environment,
    TimeProvider clock,
    ILogger<InactiveCouponCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(24));
        do
        {
            await PurgeAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task PurgeAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<ICouponRepository>();
            var settings = await repository.GetSettingsAsync(cancellationToken);
            if (!settings.InactiveCleanupEnabled)
            {
                return;
            }

            var threshold = clock.GetUtcNow().AddDays(-settings.InactiveRetentionDays);
            var images = await repository.PurgeInactiveBeforeAsync(threshold, cancellationToken);
            CouponImageFiles.Delete(environment, images, logger);

            if (images.Count > 0 && logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("保持期間を過ぎたクーポン画像を {Count} 件削除しました。", images.Count);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "クーポンの自動削除に失敗しました。");
        }
    }
}
