using CouponManager.Application;
using CouponManager.Domain;
using ZXing;
using ZXing.Common;

namespace CouponManager.Api;

internal static class CouponMediaEndpoints
{
    public static void MapCouponMediaEndpoints(this RouteGroupBuilder coupons)
    {
        coupons.MapGet("/{id:guid}/display", GetDisplayAsync);
        coupons.MapGet("/{id:guid}/image", GetImageAsync);
        coupons.MapPost("/{id:guid}/image", UploadImageAsync).DisableAntiforgery();
        coupons.MapPost("/analyze-image", AnalyzeImageAsync).DisableAntiforgery();
    }

    private static async Task<IResult> AnalyzeImageAsync(
        IFormFile file,
        OpenAiCouponImageAnalyzer analyzer,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        if (!CouponImageFiles.IsValidSize(file.Length))
        {
            return Results.BadRequest("画像は10MB以下にしてください。");
        }

        if (!CouponImageFiles.IsSupportedContentType(file.ContentType))
        {
            return Results.BadRequest("対応形式はJPEG、PNG、WebP、GIFです。");
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var analysis = await analyzer.AnalyzeAsync(stream, file.ContentType, cancellationToken);
            return Results.Ok(analysis);
        }
        catch (AiConfigurationException)
        {
            return Results.Problem(
                "AI画像解析が設定されていません。Ai:BaseUrl と Ai:Model を設定してください。",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (AiUpstreamException exception)
        {
            logger.LogWarning(exception, "OpenAI互換APIによるクーポン画像解析に失敗しました。");
            return Results.Problem(
                "AI画像解析サービスから有効な結果を取得できませんでした。",
                statusCode: StatusCodes.Status502BadGateway);
        }
    }

    private static async Task<IResult> GetDisplayAsync(
        Guid id,
        ICouponService service,
        CancellationToken cancellationToken)
    {
        var coupon = await service.FindAsync(id, cancellationToken);
        if (coupon is null)
        {
            return Results.NotFound();
        }

        if (coupon.DisplayType == CouponDisplayType.Serial)
        {
            return Results.Text(coupon.DisplayValue, "text/plain; charset=utf-8");
        }

        var format = GetBarcodeFormat(coupon);
        var (width, height) = format == BarcodeFormat.QR_CODE ? (360, 360) : (720, 240);
        var matrix = new MultiFormatWriter().encode(
            coupon.DisplayValue,
            format,
            width,
            height,
            new Dictionary<EncodeHintType, object> { [EncodeHintType.MARGIN] = 2 });

        return Results.Text(BarcodeSvgRenderer.Render(matrix), "image/svg+xml; charset=utf-8");
    }

    private static async Task<IResult> UploadImageAsync(
        Guid id,
        IFormFile file,
        IWebHostEnvironment environment,
        ICouponRepository repository,
        TimeProvider clock,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        var coupon = await repository.FindAsync(id, cancellationToken);
        if (coupon is null)
        {
            return Results.NotFound();
        }

        if (!CouponImageFiles.IsValidSize(file.Length))
        {
            return Results.BadRequest("画像は10MB以下にしてください。");
        }

        if (!CouponImageFiles.TryGetExtension(file.ContentType, out var extension))
        {
            return Results.BadRequest("対応形式はJPEG、PNG、WebP、GIFです。");
        }

        var fileName = CouponImageFiles.CreateFileName(id, extension);
        var filePath = CouponImageFiles.GetPath(environment, fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

        try
        {
            await using (var stream = File.Create(filePath))
            {
                await file.CopyToAsync(stream, cancellationToken);
            }

            var previousImage = coupon.ImagePath;
            coupon.SetImage(fileName, clock.GetUtcNow());
            await repository.SaveChangesAsync(cancellationToken);
            CouponImageFiles.Delete(environment, previousImage, logger);
            return Results.Ok(new { hasImage = true });
        }
        catch
        {
            CouponImageFiles.Delete(environment, fileName, logger);
            throw;
        }
    }

    private static async Task<IResult> GetImageAsync(
        Guid id,
        IWebHostEnvironment environment,
        ICouponRepository repository,
        CancellationToken cancellationToken)
    {
        var coupon = await repository.FindAsync(id, cancellationToken);
        if (coupon?.ImagePath is not { } imagePath)
        {
            return Results.NotFound();
        }

        var path = CouponImageFiles.GetPath(environment, imagePath);
        if (!File.Exists(path))
        {
            return Results.NotFound();
        }

        return Results.File(path, CouponImageFiles.GetContentType(path));
    }

    private static BarcodeFormat GetBarcodeFormat(CouponDto coupon) => coupon.DisplayType switch
    {
        CouponDisplayType.QrCode => BarcodeFormat.QR_CODE,
        _ when coupon.BarcodeType == CouponBarcodeType.Ean13 => BarcodeFormat.EAN_13,
        _ when coupon.BarcodeType == CouponBarcodeType.Ean8 => BarcodeFormat.EAN_8,
        _ => BarcodeFormat.CODE_128
    };
}
