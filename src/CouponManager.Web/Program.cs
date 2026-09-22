using CouponManager.Web.Components;
using CouponManager.Web.Localization;
using CouponManager.Web.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Localization;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddLocalization();
builder.Services.AddSingleton<UiText>();
builder.Services.AddScoped<ToastService>();
builder.Services.AddSingleton(TimeProvider.System);
var dataProtection = builder.Services.AddDataProtection().SetApplicationName("CouponManager");
if (builder.Configuration["DataProtectionKeysPath"] is { Length: > 0 } keysPath)
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keysPath));
builder.Services.AddHttpClient("api", client =>
    client.BaseAddress = new Uri(builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5080"));

var app = builder.Build();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
var supportedCultures = new[] { "ja", "en" };
var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture("ja")
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);
localizationOptions.RequestCultureProviders = [new CookieRequestCultureProvider()];
app.UseRequestLocalization(localizationOptions);
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context =>
    {
        if (string.Equals(context.File.Name, "service-worker.js", StringComparison.OrdinalIgnoreCase))
            context.Context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
    }
});
app.UseAntiforgery();
app.MapGet("/culture/{culture}", (string culture, string? returnUrl, HttpContext context) =>
{
    if (!supportedCultures.Contains(culture))
    {
        culture = "ja";
    }

    context.Response.Cookies.Append(CookieRequestCultureProvider.DefaultCookieName,
        CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
        new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true });
    return Results.LocalRedirect(returnUrl is { Length: > 0 } path && path.StartsWith('/') ? path : "/");
});
app.MapGet("/media/coupons/{id:guid}/{kind}", async (
    Guid id,
    string kind,
    IHttpClientFactory clients,
    CancellationToken cancellationToken) =>
{
    if (kind is not ("display" or "image"))
    {
        return Results.NotFound();
    }

    using var response = await clients.CreateClient("api")
        .GetAsync($"/api/coupons/{id}/{kind}", cancellationToken);
    if (!response.IsSuccessStatusCode)
    {
        return Results.StatusCode((int)response.StatusCode);
    }

    var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
    var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
    return Results.Bytes(bytes, contentType);
});
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();
