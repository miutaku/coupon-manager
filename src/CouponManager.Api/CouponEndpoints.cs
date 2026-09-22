using CouponManager.Application;

namespace CouponManager.Api;

public static class CouponEndpoints
{
    public static void MapCouponEndpoints(this WebApplication app)
    {
        var coupons = app.MapGroup("/api/coupons");

        coupons.MapGet("/", ListAsync);
        coupons.MapGet("/{id:guid}", GetAsync);
        coupons.MapPost("/", CreateAsync);
        coupons.MapPut("/{id:guid}", UpdateAsync);
        coupons.MapPost("/{id:guid}/used", MarkUsedAsync);
        coupons.MapPost("/{id:guid}/use-once", UseOnceAsync);
        coupons.MapPost("/bulk/used", MarkManyUsedAsync);
        coupons.MapDelete("/{id:guid}", DeleteAsync);
        coupons.MapPost("/bulk/delete", DeleteManyAsync);

        coupons.MapCouponMediaEndpoints();
    }

    public static void MapSettingsEndpoints(this WebApplication app)
    {
        app.MapGet("/api/settings", async (
                ICouponService service,
                CancellationToken cancellationToken) =>
            Results.Ok(await service.GetSettingsAsync(cancellationToken)));

        app.MapPut("/api/settings", async (
                AppSettingsDto request,
                ICouponService service,
                CancellationToken cancellationToken) =>
            Results.Ok(await service.UpdateSettingsAsync(request, cancellationToken)));

        app.MapGet("/api/notification-settings", async (
                INotificationSettingsService service,
                CancellationToken cancellationToken) =>
            Results.Ok(await service.GetAsync(cancellationToken)));

        app.MapPut("/api/notification-settings", async (
                NotificationSettingsDto request,
                INotificationSettingsService service,
                CancellationToken cancellationToken) =>
            Results.Ok(await service.UpdateAsync(request, cancellationToken)));
    }

    private static async Task<IResult> ListAsync(
        string? search,
        string? searchField,
        CouponSort? sort,
        bool? includeUsed,
        bool? includeExpired,
        ICouponService service,
        CancellationToken cancellationToken)
    {
        var query = new CouponQuery(
            search,
            searchField,
            sort ?? CouponSort.CreatedNewest,
            includeUsed ?? false,
            includeExpired ?? false);

        return Results.Ok(await service.ListAsync(query, cancellationToken));
    }

    private static async Task<IResult> GetAsync(
        Guid id,
        ICouponService service,
        CancellationToken cancellationToken)
    {
        var coupon = await service.FindAsync(id, cancellationToken);
        return coupon is null ? Results.NotFound() : Results.Ok(coupon);
    }

    private static async Task<IResult> CreateAsync(
        SaveCouponRequest request,
        ICouponService service,
        CancellationToken cancellationToken)
    {
        var coupon = await service.CreateAsync(request, cancellationToken);
        return Results.Created($"/api/coupons/{coupon.Id}", coupon);
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        SaveCouponRequest request,
        ICouponService service,
        CancellationToken cancellationToken)
    {
        var coupon = await service.UpdateAsync(id, request, cancellationToken);
        return coupon is null ? Results.NotFound() : Results.Ok(coupon);
    }

    private static async Task<IResult> MarkUsedAsync(
        Guid id,
        ICouponService service,
        CancellationToken cancellationToken) =>
        await service.MarkUsedAsync(id, cancellationToken)
            ? Results.NoContent()
            : Results.NotFound();

    private static async Task<IResult> UseOnceAsync(
        Guid id,
        ICouponService service,
        CancellationToken cancellationToken) =>
        await service.UseOnceAsync(id, cancellationToken)
            ? Results.NoContent()
            : Results.NotFound();

    private static async Task<IResult> MarkManyUsedAsync(
        BulkRequest request,
        ICouponService service,
        CancellationToken cancellationToken)
    {
        var count = await service.MarkUsedAsync(request.Ids, cancellationToken);
        return Results.Ok(new { count });
    }

    private static async Task<IResult> DeleteAsync(
        Guid id,
        ICouponService service,
        IWebHostEnvironment environment,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        var result = await service.DeleteAsync([id], cancellationToken);
        if (result.Count == 0)
        {
            return Results.NotFound();
        }

        CouponImageFiles.Delete(environment, result.ImagePaths, logger);
        return Results.NoContent();
    }

    private static async Task<IResult> DeleteManyAsync(
        BulkRequest request,
        ICouponService service,
        IWebHostEnvironment environment,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        var result = await service.DeleteAsync(request.Ids, cancellationToken);
        CouponImageFiles.Delete(environment, result.ImagePaths, logger);
        return Results.Ok(new { count = result.Count });
    }
}

public sealed record BulkRequest(IReadOnlyList<Guid> Ids);
