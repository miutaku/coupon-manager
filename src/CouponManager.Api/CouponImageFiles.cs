using System.Collections.Frozen;

namespace CouponManager.Api;

internal static class CouponImageFiles
{
    public const long MaximumSize = 10 * 1024 * 1024;

    private static readonly FrozenDictionary<string, string> Extensions =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["image/jpeg"] = ".jpg",
            ["image/png"] = ".png",
            ["image/webp"] = ".webp",
            ["image/gif"] = ".gif"
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    public static bool IsValidSize(long length) => length is > 0 and <= MaximumSize;

    public static bool IsSupportedContentType(string contentType) => Extensions.ContainsKey(contentType);

    public static bool TryGetExtension(string contentType, out string extension) =>
        Extensions.TryGetValue(contentType, out extension!);

    public static string CreateFileName(Guid couponId, string extension) =>
        $"{couponId:N}-{Guid.NewGuid():N}{extension}";

    public static string GetPath(IWebHostEnvironment environment, string fileName)
    {
        var safeFileName = Path.GetFileName(fileName);
        return Path.Combine(environment.ContentRootPath, "data", "uploads", safeFileName);
    }

    public static string GetContentType(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".webp" => "image/webp",
        ".gif" => "image/gif",
        _ => "application/octet-stream"
    };

    public static void Delete(
        IWebHostEnvironment environment,
        IEnumerable<string> imagePaths,
        ILogger logger)
    {
        foreach (var imagePath in imagePaths)
        {
            Delete(environment, imagePath, logger);
        }
    }

    public static void Delete(IWebHostEnvironment environment, string? imagePath, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            return;
        }

        var path = GetPath(environment, imagePath);
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(exception, "クーポン画像 {ImagePath} を削除できませんでした。", imagePath);
        }
    }
}
