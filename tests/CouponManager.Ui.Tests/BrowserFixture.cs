using Microsoft.Playwright;
using Xunit;

namespace CouponManager.Ui.Tests;

public sealed class BrowserFixture : IAsyncLifetime
{
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    public string? BaseUrl { get; } = GetBaseUrl();

    public async Task InitializeAsync()
    {
        if (BaseUrl is null)
        {
            return;
        }

        _playwright = await Playwright.CreateAsync();
        var executablePath = Environment.GetEnvironmentVariable("PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH");
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            ExecutablePath = string.IsNullOrWhiteSpace(executablePath) ? null : executablePath
        });
    }

    public async Task<IPage?> CreatePageAsync(ViewportSize? viewportSize = null)
    {
        if (_browser is null)
        {
            return null;
        }

        var context = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = viewportSize
        });
        return await context.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        if (_browser is not null)
        {
            await _browser.DisposeAsync();
        }

        _playwright?.Dispose();
    }

    private static string? GetBaseUrl()
    {
        var value = Environment.GetEnvironmentVariable("E2E_BASE_URL");
        return string.IsNullOrWhiteSpace(value) ? null : value.TrimEnd('/');
    }
}
