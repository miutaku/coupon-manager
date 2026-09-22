using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using CouponManager.Application;
using CouponManager.Domain;

namespace CouponManager.Api;

public sealed record NotificationMessage(string Type, Guid CouponId, string CouponName,
    DateOnly? ExpiresOn, string Title, string Body, string Url);
public sealed record PendingNotification(string EventKey, NotificationMessage Message);

public interface INotificationEventSource
{
    string Type { get; }
    Task<IReadOnlyList<PendingNotification>> GetDueAsync(NotificationRule rule, DateTimeOffset now,
        CancellationToken cancellationToken);
}

public interface INotificationChannel
{
    string Type { get; }
    Task SendAsync(NotificationMessage message, IReadOnlyDictionary<string, string> configuration,
        CancellationToken cancellationToken);
}

public sealed class CouponExpirationNotificationSource(
    INotificationRepository repository,
    IConfiguration configuration,
    ILogger<CouponExpirationNotificationSource> logger) : INotificationEventSource
{
    private readonly INotificationRepository repository = repository;
    private readonly TimeZoneInfo timeZone = ResolveTimeZone(configuration["Notifications:TimeZone"], logger);

    public string Type => NotificationTypes.CouponExpiration;

    public async Task<IReadOnlyList<PendingNotification>> GetDueAsync(NotificationRule rule, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var localNow = TimeZoneInfo.ConvertTime(now, timeZone);
        var from = DateOnly.FromDateTime(localNow.DateTime);
        var through = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now.Add(rule.LeadTime), timeZone).DateTime);
        var coupons = await repository.ListExpiringCouponsAsync(from, through, cancellationToken);
        var notifications = new List<PendingNotification>(coupons.Count);

        foreach (var coupon in coupons)
        {
            var expiresOn = coupon.ExpiresOn!.Value;
            var localExpiry = DateTime.SpecifyKind(expiresOn.ToDateTime(TimeOnly.MaxValue), DateTimeKind.Unspecified);
            var expiresAt = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localExpiry, timeZone), TimeSpan.Zero);
            if (now < expiresAt - rule.LeadTime || now > expiresAt)
            {
                continue;
            }

            var leadTimeText = FormatLeadTime(rule);
            var message = new NotificationMessage(Type, coupon.Id, coupon.Name, expiresOn,
                "クーポンの有効期限が近づいています",
                $"{coupon.Name} は {expiresOn:yyyy-MM-dd} に期限切れになります（{leadTimeText}）。",
                $"/coupons/{coupon.Id}/edit");
            var eventKey = $"{Type}:{coupon.Id:N}:{expiresOn:yyyyMMdd}:{rule.LeadTimeDays}:{rule.LeadTimeHours}";
            notifications.Add(new PendingNotification(eventKey, message));
        }

        return notifications;
    }

    private static string FormatLeadTime(NotificationRule rule)
    {
        var days = rule.LeadTimeDays > 0 ? $"{rule.LeadTimeDays}日" : string.Empty;
        var hours = rule.LeadTimeHours > 0 ? $"{rule.LeadTimeHours}時間" : string.Empty;
        return $"{days}{hours}前";
    }

    private static TimeZoneInfo ResolveTimeZone(
        string? configuredId,
        ILogger<CouponExpirationNotificationSource> logger)
    {
        var timeZoneId = string.IsNullOrWhiteSpace(configuredId) ? "UTC" : configuredId;
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            logger.LogWarning("通知タイムゾーン {TimeZoneId} が無効なためUTCを使用します。", timeZoneId);
            return TimeZoneInfo.Utc;
        }
    }
}

public sealed class DiscordNotificationChannel(HttpClient httpClient) : INotificationChannel
{
    public string Type => NotificationTypes.Discord;

    public async Task SendAsync(NotificationMessage message, IReadOnlyDictionary<string, string> configuration,
        CancellationToken cancellationToken)
    {
        if (!configuration.TryGetValue("webhookUrl", out var value) ||
            !Uri.TryCreate(value, UriKind.Absolute, out var webhook) || webhook.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException("Discord Webhook URLが設定されていません。");
        }

        using var response = await httpClient.PostAsJsonAsync(webhook, new { content = $"**{message.Title}**\n{message.Body}" }, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}

public sealed class ShellScriptNotificationChannel(ILogger<ShellScriptNotificationChannel> logger) : INotificationChannel
{
    public string Type => NotificationTypes.ShellScript;

    public async Task SendAsync(NotificationMessage message, IReadOnlyDictionary<string, string> configuration,
        CancellationToken cancellationToken)
    {
        if (!configuration.TryGetValue("executablePath", out var path) || string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException("通知スクリプトのパスが設定されていません。");
        }

        var start = new ProcessStartInfo(path) { UseShellExecute = false, RedirectStandardError = true };
        start.Environment["COUPON_NOTIFICATION_TYPE"] = message.Type;
        start.Environment["COUPON_ID"] = message.CouponId.ToString();
        start.Environment["COUPON_NAME"] = message.CouponName;
        start.Environment["COUPON_EXPIRES_ON"] = message.ExpiresOn?.ToString("yyyy-MM-dd") ?? "";
        start.Environment["COUPON_NOTIFICATION_TITLE"] = message.Title;
        start.Environment["COUPON_NOTIFICATION_BODY"] = message.Body;
        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("通知スクリプトを開始できませんでした。");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        var standardError = process.StandardError.ReadToEndAsync(timeout.Token);
        await process.WaitForExitAsync(timeout.Token);
        var error = await standardError;
        if (process.ExitCode != 0)
        {
            logger.LogWarning("通知スクリプトが終了コード {ExitCode} で失敗しました: {Error}", process.ExitCode, error);
            throw new InvalidOperationException($"通知スクリプトが終了コード {process.ExitCode} で失敗しました。");
        }
    }
}

public sealed class NotificationDispatchService(IServiceScopeFactory scopeFactory, TimeProvider clock,
    ILogger<NotificationDispatchService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(15));
        do
        {
            await DispatchAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task DispatchAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var services = scope.ServiceProvider;
            var repository = services.GetRequiredService<INotificationRepository>();
            var sources = services.GetServices<INotificationEventSource>().ToDictionary(source => source.Type);
            var channels = services.GetServices<INotificationChannel>().ToDictionary(channel => channel.Type);
            var enabledChannels = (await repository.ListChannelsAsync(cancellationToken))
                .Where(channel => channel.Enabled && channels.ContainsKey(channel.Type))
                .ToList();
            var now = clock.GetUtcNow();

            foreach (var rule in (await repository.ListRulesAsync(cancellationToken)).Where(rule => rule.Enabled))
            {
                if (!sources.TryGetValue(rule.Type, out var source))
                {
                    continue;
                }

                var pending = await source.GetDueAsync(rule, now, cancellationToken);
                await DispatchPendingAsync(repository, channels, enabledChannels, pending, cancellationToken);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "通知処理に失敗しました。");
        }
    }

    private async Task DispatchPendingAsync(
        INotificationRepository repository,
        Dictionary<string, INotificationChannel> channels,
        IReadOnlyList<NotificationChannelSetting> settings,
        IReadOnlyList<PendingNotification> pendingNotifications,
        CancellationToken cancellationToken)
    {
        var deliveries = pendingNotifications
            .SelectMany(notification => settings.Select(setting => new PendingDelivery(
                $"{notification.EventKey}:{setting.Type}", notification, setting)))
            .ToList();
        var deliveredKeys = await repository.ListDeliveredEventKeysAsync(
            deliveries.Select(delivery => delivery.EventKey), cancellationToken);

        foreach (var delivery in deliveries.Where(delivery => !deliveredKeys.Contains(delivery.EventKey)))
        {
            var channel = channels[delivery.Setting.Type];
            try
            {
                var configuration = JsonSerializer.Deserialize<Dictionary<string, string>>(
                    delivery.Setting.ConfigurationJson) ?? [];
                await channel.SendAsync(delivery.Notification.Message, configuration, cancellationToken);
                await repository.AddDeliveryAsync(
                    new NotificationDelivery(delivery.EventKey, clock.GetUtcNow()), cancellationToken);
                await repository.SaveChangesAsync(cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "{Channel} への通知送信に失敗しました。", delivery.Setting.Type);
            }
        }
    }

    private sealed record PendingDelivery(
        string EventKey,
        PendingNotification Notification,
        NotificationChannelSetting Setting);
}
