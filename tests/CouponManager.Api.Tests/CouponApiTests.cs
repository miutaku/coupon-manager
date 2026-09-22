using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using CouponManager.Api;
using CouponManager.Application;
using CouponManager.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;
using Xunit;

namespace CouponManager.Api.Tests;

public sealed class DatabaseFixture : IAsyncLifetime
{
    public PostgreSqlContainer Database { get; } = new PostgreSqlBuilder("postgres:18.6-alpine")
        .WithDatabase("coupons_test").WithUsername("coupons").WithPassword("test-password").Build();
    public Task InitializeAsync() => Database.StartAsync();
    public Task DisposeAsync() => Database.DisposeAsync().AsTask();
}

public sealed class CouponApiFactory(DatabaseFixture fixture) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Coupons", fixture.Database.GetConnectionString());
        builder.UseSetting("Database:MigrateOnStartup", "true");
    }
}

public sealed class CouponApiTests(DatabaseFixture database) : IClassFixture<DatabaseFixture>
{
    [Fact]
    public async Task Image_analysis_requires_an_ai_endpoint()
    {
        await using var factory = new CouponApiFactory(database);
        using var client = factory.CreateClient();
        using var form = new MultipartFormDataContent();
        using var image = new ByteArrayContent([137, 80, 78, 71]);
        image.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(image, "file", "coupon.png");

        var response = await client.PostAsync("/api/coupons/analyze-image", form);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Can_create_and_read_coupon()
    {
        await using var factory = new CouponApiFactory(database);
        using var client = factory.CreateClient();
        var request = new SaveCouponRequest("Coffee", "Morning only", null, 2, null,
            CouponDisplayType.Serial, CouponBarcodeType.Code128, "COFFEE20", [new LabelDto("店舗", "Cafe")]);

        var created = await client.PostAsJsonAsync("/api/coupons", request);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var coupon = await created.Content.ReadFromJsonAsync<CouponDto>();
        Assert.NotNull(coupon);
        var coupons = await client.GetFromJsonAsync<List<CouponDto>>("/api/coupons");
        Assert.Contains(coupons!, x => x.Name == "Coffee" && x.RemainingUses == 2);

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"/api/coupons/{coupon.Id}/use-once", null)).StatusCode);
        Assert.Equal(1, (await client.GetFromJsonAsync<CouponDto>($"/api/coupons/{coupon.Id}"))!.RemainingUses);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/coupons/{coupon.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/coupons/{coupon.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/coupons/trash")).StatusCode);
    }

    [Fact]
    public async Task Automatic_cleanup_can_be_disabled_without_losing_the_retention_period()
    {
        await using var factory = new CouponApiFactory(database);
        using var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync("/api/settings", new AppSettingsDto(60, false));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var settings = await response.Content.ReadFromJsonAsync<AppSettingsDto>();
        Assert.NotNull(settings);
        Assert.False(settings.InactiveCleanupEnabled);
        Assert.Equal(60, settings.InactiveRetentionDays);
    }

    [Fact]
    public async Task Notification_rules_and_channels_are_configurable()
    {
        await using var factory = new CouponApiFactory(database);
        using var client = factory.CreateClient();
        var request = new NotificationSettingsDto(
            [new(NotificationTypes.CouponExpiration, true, 2, 12)],
            [
                new(NotificationTypes.Discord, false, new Dictionary<string, string> { ["webhookUrl"] = "https://discord.example/webhook" }),
                new(NotificationTypes.ShellScript, false, new Dictionary<string, string> { ["executablePath"] = "/notify.sh" })
            ]);

        var response = await client.PutAsJsonAsync("/api/notification-settings", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var settings = await response.Content.ReadFromJsonAsync<NotificationSettingsDto>();
        var expiry = Assert.Single(settings!.Rules, x => x.Type == NotificationTypes.CouponExpiration);
        Assert.True(expiry.Enabled);
        Assert.Equal(2, expiry.LeadTimeDays);
        Assert.Equal(12, expiry.LeadTimeHours);
        Assert.Contains(settings.Channels, x => x.Type == NotificationTypes.Discord && x.Configuration["webhookUrl"].Contains("discord"));
    }

    [Fact]
    public async Task Bulk_mark_used_ignores_duplicate_ids_and_updates_all_matching_coupons()
    {
        await using var factory = new CouponApiFactory(database);
        using var client = factory.CreateClient();
        var request = new SaveCouponRequest("Bulk test", null, null, 2, null,
            CouponDisplayType.Serial, CouponBarcodeType.Code128, "BULK", []);
        var firstResponse = await client.PostAsJsonAsync("/api/coupons", request);
        var secondResponse = await client.PostAsJsonAsync("/api/coupons", request);
        var first = await firstResponse.Content.ReadFromJsonAsync<CouponDto>();
        var second = await secondResponse.Content.ReadFromJsonAsync<CouponDto>();
        Assert.NotNull(first);
        Assert.NotNull(second);

        var response = await client.PostAsJsonAsync("/api/coupons/bulk/used",
            new { ids = new[] { first.Id, first.Id, second.Id } });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, (await client.GetFromJsonAsync<CouponDto>($"/api/coupons/{first.Id}"))!.RemainingUses);
        Assert.Equal(0, (await client.GetFromJsonAsync<CouponDto>($"/api/coupons/{second.Id}"))!.RemainingUses);
    }
}

public sealed class OpenAiCouponImageAnalyzerTests
{
    [Fact]
    public async Task Sends_the_image_to_an_openai_compatible_endpoint_and_maps_the_result()
    {
        var handler = new StubHandler(async request =>
        {
            Assert.Equal("http://ai.test/v1/chat/completions", request.RequestUri?.ToString());
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("secret", request.Headers.Authorization?.Parameter);
            var body = await request.Content!.ReadAsStringAsync();
            Assert.Contains("data:image/png;base64,iVBORw==", body);
            Assert.Contains("json_schema", body);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    {"choices":[{"message":{"content":"{\"coupons\":[{\"name\":\"20% OFF\",\"description\":\"One item\",\"expiresOn\":\"2026-12-31\",\"totalUses\":1,\"displayType\":\"Barcode\",\"barcodeType\":\"Ean13\",\"displayValue\":\"4901234567894\"}]}"}}]}
                    """, Encoding.UTF8, "application/json")
            };
        });
        using var client = new HttpClient(handler);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Ai:BaseUrl"] = "http://ai.test/v1",
            ["Ai:ApiKey"] = "secret",
            ["Ai:Model"] = "vision-model"
        }).Build();
        var analyzer = new OpenAiCouponImageAnalyzer(client, configuration);
        await using var image = new MemoryStream([137, 80, 78, 71]);

        var result = await analyzer.AnalyzeAsync(image, "image/png", CancellationToken.None);

        var coupon = Assert.Single(result.Coupons);
        Assert.Equal("20% OFF", coupon.Name);
        Assert.Equal(new DateOnly(2026, 12, 31), coupon.ExpiresOn);
        Assert.Equal(CouponDisplayType.Barcode, coupon.DisplayType);
        Assert.Equal(CouponBarcodeType.Ean13, coupon.BarcodeType);
        Assert.Equal("4901234567894", coupon.DisplayValue);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> response)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) => response(request);
    }
}

public sealed class NotificationChannelTests
{
    [Fact]
    public async Task Discord_channel_posts_a_webhook_message()
    {
        string? body = null;
        var handler = new NotificationStubHandler(async request =>
        {
            body = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });
        using var client = new HttpClient(handler);
        var channel = new DiscordNotificationChannel(client);
        var message = new NotificationMessage(NotificationTypes.CouponExpiration, Guid.NewGuid(), "Coffee",
            new DateOnly(2026, 12, 31), "Expiry", "Coffee expires soon", "/");

        await channel.SendAsync(message,
            new Dictionary<string, string> { ["webhookUrl"] = "https://discord.example/webhook" }, CancellationToken.None);

        Assert.Contains("Coffee expires soon", body);
    }

    [Fact]
    public async Task Shell_channel_executes_an_absolute_program()
    {
        var channel = new ShellScriptNotificationChannel(Microsoft.Extensions.Logging.Abstractions.NullLogger<ShellScriptNotificationChannel>.Instance);
        var message = new NotificationMessage(NotificationTypes.CouponExpiration, Guid.NewGuid(), "Coffee",
            new DateOnly(2026, 12, 31), "Expiry", "Coffee expires soon", "/");

        await channel.SendAsync(message,
            new Dictionary<string, string> { ["executablePath"] = "/bin/true" }, CancellationToken.None);
    }

    private sealed class NotificationStubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> response)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) => response(request);
    }
}
