using System.Text.Json;
using CouponManager.Domain;

namespace CouponManager.Application;

public sealed class NotificationSettingsService(INotificationRepository repository) : INotificationSettingsService
{
    public async Task<NotificationSettingsDto> GetAsync(CancellationToken cancellationToken) =>
        Map(await repository.ListRulesAsync(cancellationToken), await repository.ListChannelsAsync(cancellationToken));

    public async Task<NotificationSettingsDto> UpdateAsync(NotificationSettingsDto request, CancellationToken cancellationToken)
    {
        foreach (var channel in request.Channels)
        {
            ValidateChannel(channel);
        }

        var rules = await repository.ListRulesAsync(cancellationToken);
        var channels = await repository.ListChannelsAsync(cancellationToken);
        var rulesByType = rules.ToDictionary(rule => rule.Type, StringComparer.Ordinal);
        var channelsByType = channels.ToDictionary(channel => channel.Type, StringComparer.Ordinal);

        foreach (var input in request.Rules)
        {
            if (!rulesByType.TryGetValue(input.Type, out var rule))
            {
                continue;
            }

            rule.Update(input.Type, input.Enabled, input.LeadTimeDays, input.LeadTimeHours);
        }

        foreach (var input in request.Channels)
        {
            if (!channelsByType.TryGetValue(input.Type, out var channel))
            {
                continue;
            }

            channel.Update(input.Type, input.Enabled, JsonSerializer.Serialize(input.Configuration));
        }

        await repository.SaveChangesAsync(cancellationToken);
        return Map(rules, channels);
    }

    private static NotificationSettingsDto Map(IReadOnlyList<NotificationRule> rules,
        IReadOnlyList<NotificationChannelSetting> channels) => new(
        [.. rules.Select(rule => new NotificationRuleDto(
            rule.Type, rule.Enabled, rule.LeadTimeDays, rule.LeadTimeHours))],
        [.. channels.Select(channel => new NotificationChannelDto(
            channel.Type,
            channel.Enabled,
            JsonSerializer.Deserialize<Dictionary<string, string>>(channel.ConfigurationJson) ?? []))]);

    private static void ValidateChannel(NotificationChannelDto channel)
    {
        if (!channel.Enabled)
        {
            return;
        }

        if (channel.Type == NotificationTypes.Discord &&
            (!channel.Configuration.TryGetValue("webhookUrl", out var webhook) ||
             !Uri.TryCreate(webhook, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("Discord Webhook URLにはHTTPSのURLを指定してください。");
        }

        if (channel.Type == NotificationTypes.ShellScript &&
            (!channel.Configuration.TryGetValue("executablePath", out var path) || !Path.IsPathFullyQualified(path)))
        {
            throw new ArgumentException("シェルスクリプトにはAPIコンテナ内の絶対パスを指定してください。");
        }
    }
}
