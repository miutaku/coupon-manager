using Microsoft.Playwright;
using System.Text.RegularExpressions;
using Xunit;

namespace CouponManager.Ui.Tests;

public sealed partial class MainFlowTests(BrowserFixture browser) : IClassFixture<BrowserFixture>
{
    [Fact]
    public async Task App_is_installable_as_a_pwa()
    {
        var page = await browser.CreatePageAsync();
        if (page is null)
        {
            return;
        }

        var baseUrl = browser.BaseUrl!;
        await page.GotoAsync(baseUrl);

        await Assertions.Expect(page.Locator("link[rel='manifest']")).ToHaveAttributeAsync("href", "manifest.webmanifest");
        var manifest = await page.EvaluateAsync<string>("async () => JSON.stringify(await (await fetch('/manifest.webmanifest')).json())");
        Assert.Contains("\"display\":\"standalone\"", manifest);
        Assert.Contains("app-icon-192.png", manifest);
        Assert.Contains("app-icon-512.png", manifest);
        Assert.True(await page.EvaluateAsync<bool>("async () => { const registration = await navigator.serviceWorker.ready; return registration.active?.scriptURL.endsWith('/service-worker.js') === true; }"));
        await page.ReloadAsync();
        Assert.True(await page.EvaluateAsync<bool>("() => navigator.serviceWorker.controller !== null"));

        await page.Context.SetOfflineAsync(true);
        try
        {
            await page.GotoAsync($"{baseUrl.TrimEnd('/')}/settings?offline-test=1");
            await Assertions.Expect(page.Locator("#title")).ToContainTextAsync(OfflinePageRegex());
        }
        finally
        {
            await page.Context.SetOfflineAsync(false);
        }
    }

    [Fact]
    public async Task User_can_open_list_and_add_form()
    {
        var page = await browser.CreatePageAsync();
        if (page is null)
        {
            return;
        }

        var baseUrl = browser.BaseUrl!;
        await page.GotoAsync(baseUrl);
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "クーポン", Exact = true })).ToBeVisibleAsync();
        await page.Locator("a[href='/coupons/new']").First.ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "クーポンを追加" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Changing_language_reloads_the_page_in_the_selected_culture()
    {
        var page = await browser.CreatePageAsync();
        if (page is null)
        {
            return;
        }

        var baseUrl = browser.BaseUrl!;
        await page.GotoAsync($"{baseUrl.TrimEnd('/')}/settings");
        await page.GetByRole(AriaRole.Link, new() { Name = "English", Exact = true }).ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Settings", Exact = true })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Selected_theme_survives_enhanced_navigation()
    {
        var page = await browser.CreatePageAsync();
        if (page is null)
        {
            return;
        }

        var baseUrl = browser.BaseUrl!;
        await page.EmulateMediaAsync(new() { ColorScheme = ColorScheme.Dark });
        await page.GotoAsync($"{baseUrl.TrimEnd('/')}/settings");

        await page.GetByRole(AriaRole.Button, new() { Name = "ライト" }).ClickAsync();
        await Assertions.Expect(page.Locator("html")).ToHaveAttributeAsync("data-theme", "light");
        await page.Locator("a[href='/coupons/new']").First.ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "クーポンを追加" })).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("html")).ToHaveAttributeAsync("data-theme", "light");

        await page.ReloadAsync();
        await Assertions.Expect(page.Locator("html")).ToHaveAttributeAsync("data-theme", "light");
    }

    [Fact]
    public async Task Automatic_cleanup_can_be_disabled()
    {
        var page = await browser.CreatePageAsync();
        if (page is null)
        {
            return;
        }

        var baseUrl = browser.BaseUrl!;
        await page.GotoAsync($"{baseUrl.TrimEnd('/')}/settings");

        var toggle = page.Locator(".cleanup-toggle");
        var toggleInput = toggle.Locator("input");
        var days = page.GetByLabel("自動削除");
        var originallyEnabled = await toggleInput.IsCheckedAsync();
        try
        {
            if (!originallyEnabled)
            {
                await toggleInput.EvaluateAsync("element => element.click()");
                await page.GetByRole(AriaRole.Button, new() { Name = "設定を保存", Exact = true }).ClickAsync();
                await Assertions.Expect(page.Locator(".toast")).ToContainTextAsync("設定を保存しました");
            }

            await toggleInput.EvaluateAsync("element => element.click()");
            await Assertions.Expect(days).ToBeDisabledAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "設定を保存", Exact = true }).ClickAsync();
            await Assertions.Expect(page.Locator(".toast")).ToContainTextAsync("設定を保存しました");
            await page.ReloadAsync();
            await Assertions.Expect(toggleInput).Not.ToBeCheckedAsync();
            await Assertions.Expect(days).ToBeDisabledAsync();
        }
        finally
        {
            if (await toggleInput.IsCheckedAsync() != originallyEnabled)
            {
                await toggleInput.EvaluateAsync("element => element.click()");
                await page.GetByRole(AriaRole.Button, new() { Name = "設定を保存", Exact = true }).ClickAsync();
            }
        }
    }

    [Fact]
    public async Task Notification_settings_offer_expiry_discord_and_shell_channels()
    {
        var page = await browser.CreatePageAsync();
        if (page is null)
        {
            return;
        }

        var baseUrl = browser.BaseUrl!;
        await page.GotoAsync($"{baseUrl.TrimEnd('/')}/settings");

        await Assertions.Expect(page.Locator(".notification-card")).ToContainTextAsync("クーポンの有効期限");
        await Assertions.Expect(page.GetByLabel("期限の何日前に通知するか")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByLabel("期限の何時間前に通知するか")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByLabel("Discord Webhook URL")).ToBeAttachedAsync();
        await Assertions.Expect(page.GetByLabel("シェルスクリプトのパス")).ToBeAttachedAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "通知設定を保存", Exact = true }).ClickAsync();
        await Assertions.Expect(page.Locator(".toast")).ToContainTextAsync("通知設定を保存しました。");
        await Assertions.Expect(page.Locator(".toast")).ToHaveAttributeAsync("role", "status");
    }

    [Fact]
    public async Task Bulk_actions_appear_directly_above_bottom_navigation()
    {
        var page = await browser.CreatePageAsync(new ViewportSize
        {
            Width = 430,
            Height = 932
        });
        if (page is null)
        {
            return;
        }

        await page.GotoAsync(browser.BaseUrl!);
        if (await page.Locator(".wallet-card").CountAsync() == 0)
        {
            return;
        }

        await page.WaitForTimeoutAsync(600);

        var searchBox = await page.Locator(".search-box").BoundingBoxAsync();
        var selectionButton = page.GetByTitle("複数選択");
        var selectionButtonBox = await selectionButton.BoundingBoxAsync();
        var filterOptions = await page.Locator(".filter-options").BoundingBoxAsync();
        Assert.NotNull(searchBox);
        Assert.NotNull(selectionButtonBox);
        Assert.NotNull(filterOptions);
        Assert.True(selectionButtonBox.Y >= searchBox.Y + searchBox.Height);
        Assert.True(selectionButtonBox.Y >= filterOptions.Y && selectionButtonBox.Y + selectionButtonBox.Height <= filterOptions.Y + filterOptions.Height);

        await selectionButton.ClickAsync();
        await page.Locator(".select-coupon").First.ClickAsync();
        var actions = await page.Locator(".bulk-bar").BoundingBoxAsync();
        var navigation = await page.Locator(".bottom-nav").BoundingBoxAsync();

        Assert.NotNull(actions);
        Assert.NotNull(navigation);
        var gap = navigation.Y - (actions.Y + actions.Height);
        Assert.True(gap is >= 0 and <= 20, $"actions={actions.Y}/{actions.Height}, navigation={navigation.Y}, gap={gap}");
    }

    [Fact]
    public async Task Used_and_expired_coupon_actions_remain_enabled()
    {
        var page = await browser.CreatePageAsync();
        if (page is null)
        {
            return;
        }

        var baseUrl = browser.BaseUrl!;
        await page.GotoAsync(baseUrl);
        await page.Locator(".switch").ClickAsync();

        var inactiveCard = page.Locator(".wallet-card.used, .wallet-card.expired").First;
        if (await inactiveCard.CountAsync() == 0)
        {
            return;
        }

        var buttons = inactiveCard.Locator(".card-actions button");
        for (var index = 0; index < await buttons.CountAsync(); index++)
        {
            await Assertions.Expect(buttons.Nth(index)).ToBeEnabledAsync();
        }
    }

    [Fact]
    public async Task Code_selection_and_label_removal_are_animated()
    {
        var page = await browser.CreatePageAsync();
        if (page is null)
        {
            return;
        }

        var baseUrl = browser.BaseUrl!;
        await page.GotoAsync($"{baseUrl.TrimEnd('/')}/coupons/new");

        var barcode = page.Locator(".type-option").Filter(new() { HasText = "バーコード" });
        await barcode.ClickAsync();
        await Assertions.Expect(barcode).ToHaveClassAsync(ActiveClassRegex());
        var selectionAnimations = await barcode.EvaluateAsync<int>("element => element.getAnimations().length");
        Assert.True(selectionAnimations > 0);

        await page.Locator(".add-label").ClickAsync();
        var labels = page.Locator(".label-pair");
        await Assertions.Expect(labels).ToHaveCountAsync(2);
        await labels.First.Locator(".remove-label").ClickAsync();
        await Assertions.Expect(labels.First).ToHaveClassAsync(RemovingClassRegex());
        await Assertions.Expect(labels).ToHaveCountAsync(1);
    }

    [Fact]
    public async Task Image_upload_control_explains_ai_processing()
    {
        var page = await browser.CreatePageAsync();
        if (page is null)
        {
            return;
        }

        var baseUrl = browser.BaseUrl!;
        await page.GotoAsync($"{baseUrl.TrimEnd('/')}/coupons/new");
        await Assertions.Expect(page.Locator("#coupon-image-input")).ToBeAttachedAsync();
        await Assertions.Expect(page.Locator(".upload-box")).ToContainTextAsync("クーポン画像から自動入力");
        await Assertions.Expect(page.Locator(".image-retention-option input")).ToBeCheckedAsync();
    }

    [Fact]
    public async Task Generated_code_and_uploaded_image_load_inside_the_modal()
    {
        var page = await browser.CreatePageAsync();
        if (page is null)
        {
            return;
        }

        var baseUrl = browser.BaseUrl!;
        await page.GotoAsync(baseUrl);
        var card = page.Locator(".wallet-card").Filter(new() { HasText = "てすと" }).First;
        if (await card.CountAsync() == 0 ||
            await card.Locator("button[title='画像']").CountAsync() == 0)
        {
            return;
        }

        await card.Locator(".card-main-action").ClickAsync();
        var code = page.Locator(".modal .coupon-code");
        await Assertions.Expect(code).ToBeVisibleAsync();
        Assert.True(await code.EvaluateAsync<bool>("image => image.complete && image.naturalWidth > 0"));
        await page.Locator(".modal-close").ClickAsync();

        await card.Locator("button[title='画像']").ClickAsync();
        var image = page.Locator(".modal .coupon-code");
        await Assertions.Expect(image).ToBeVisibleAsync();
        Assert.True(await image.EvaluateAsync<bool>("element => element.complete && element.naturalWidth > 0"));
    }

    [GeneratedRegex("オフライン|offline", RegexOptions.IgnoreCase, "")]
    private static partial Regex OfflinePageRegex();

    [GeneratedRegex("active")]
    private static partial Regex ActiveClassRegex();

    [GeneratedRegex("removing")]
    private static partial Regex RemovingClassRegex();
}
