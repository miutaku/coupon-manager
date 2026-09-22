using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using CouponManager.Application;
using CouponManager.Domain;

namespace CouponManager.Api;

public sealed class OpenAiCouponImageAnalyzer(HttpClient httpClient, IConfiguration configuration)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<CouponImageAnalysisDto> AnalyzeAsync(
        Stream image, string contentType, CancellationToken cancellationToken)
    {
        var baseUrl = configuration["Ai:BaseUrl"];
        var model = configuration["Ai:Model"];
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(model))
        {
            throw new AiConfigurationException();
        }

        if (!Uri.TryCreate(baseUrl.TrimEnd('/') + "/chat/completions", UriKind.Absolute, out var endpoint))
        {
            throw new AiConfigurationException();
        }

        using var memory = new MemoryStream();
        await image.CopyToAsync(memory, cancellationToken);
        memory.TryGetBuffer(out var imageBuffer);
        var base64Image = Convert.ToBase64String(imageBuffer.Array!, imageBuffer.Offset, imageBuffer.Count);
        var dataUrl = $"data:{contentType};base64,{base64Image}";
        var payload = CreateRequest(model, dataUrl);

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(payload)
        };
        if (configuration["Ai:ApiKey"] is { Length: > 0 } apiKey)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }

        var timeoutSeconds = Math.Clamp(configuration.GetValue("Ai:TimeoutSeconds", 180), 10, 900);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        using var response = await SendAsync(request, timeout.Token, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new AiUpstreamException($"The AI service returned HTTP {(int)response.StatusCode}.");
        }

        using var document = await ReadDocumentAsync(response, cancellationToken);
        var content = ReadMessageContent(document.RootElement);
        return ParseAnalysis(content);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken timeoutToken,
        CancellationToken callerToken)
    {
        try
        {
            return await httpClient.SendAsync(request, timeoutToken);
        }
        catch (OperationCanceledException) when (!callerToken.IsCancellationRequested)
        {
            throw new AiUpstreamException("The AI service timed out.");
        }
        catch (HttpRequestException exception)
        {
            throw new AiUpstreamException("The AI service could not be reached.", exception);
        }
    }

    private static async Task<JsonDocument> ReadDocumentAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<JsonDocument>(JsonOptions, cancellationToken)
                ?? throw new JsonException("The AI service returned an empty response.");
        }
        catch (JsonException exception)
        {
            throw new AiUpstreamException("The AI service returned invalid JSON.", exception);
        }
    }

    private static JsonObject CreateRequest(string model, string dataUrl) => new()
    {
        ["model"] = model,
        ["temperature"] = 0,
        ["max_tokens"] = 1200,
        ["messages"] = new JsonArray
        {
            new JsonObject
            {
                ["role"] = "system",
                ["content"] = """
                    You extract coupon information from images. The image may be a standalone coupon,
                    a receipt containing one or more coupons, or a noisy document. Find every actual coupon
                    and ignore purchases, totals, payments, store metadata, advertisements, and other unrelated text.
                    Read coupon text directly from the image. Never invent unreadable values. Return only JSON
                    matching the requested schema. Dates must use yyyy-MM-dd. totalUses defaults to 1 only when
                    a coupon is clearly present. displayType is QrCode, Barcode, or Serial. barcodeType is Code128,
                    Ean13, or Ean8 and is relevant only to Barcode. Preserve code values exactly as seen.
                    """
            },
            new JsonObject
            {
                ["role"] = "user",
                ["content"] = new JsonArray
                {
                    new JsonObject { ["type"] = "text", ["text"] = "Extract all coupon candidates from this image." },
                    new JsonObject
                    {
                        ["type"] = "image_url",
                        ["image_url"] = new JsonObject { ["url"] = dataUrl, ["detail"] = "high" }
                    }
                }
            }
        },
        ["response_format"] = new JsonObject
        {
            ["type"] = "json_schema",
            ["json_schema"] = new JsonObject
            {
                ["name"] = "coupon_image_analysis",
                ["strict"] = true,
                ["schema"] = CreateResponseSchema()
            }
        }
    };

    private static JsonObject CreateResponseSchema() => new()
    {
        ["type"] = "object",
        ["additionalProperties"] = false,
        ["required"] = new JsonArray("coupons"),
        ["properties"] = new JsonObject
        {
            ["coupons"] = new JsonObject
            {
                ["type"] = "array",
                ["items"] = new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("name", "description", "expiresOn", "totalUses", "displayType", "barcodeType", "displayValue"),
                    ["properties"] = new JsonObject
                    {
                        ["name"] = NullableString(),
                        ["description"] = NullableString(),
                        ["expiresOn"] = NullableString(),
                        ["totalUses"] = new JsonObject { ["type"] = new JsonArray("integer", "null") },
                        ["displayType"] = new JsonObject { ["enum"] = new JsonArray("QrCode", "Barcode", "Serial", null) },
                        ["barcodeType"] = new JsonObject { ["enum"] = new JsonArray("Code128", "Ean13", "Ean8", null) },
                        ["displayValue"] = NullableString()
                    }
                }
            }
        }
    };

    private static JsonObject NullableString() => new()
    {
        ["type"] = new JsonArray("string", "null")
    };

    private static string ReadMessageContent(JsonElement root)
    {
        if (!root.TryGetProperty("choices", out var choices) ||
            choices.ValueKind != JsonValueKind.Array ||
            choices.GetArrayLength() == 0 ||
            !choices[0].TryGetProperty("message", out var message) ||
            !message.TryGetProperty("content", out var content))
        {
            throw new AiUpstreamException("The AI service response did not contain a completion.");
        }

        if (content.ValueKind == JsonValueKind.String)
        {
            return content.GetString() ?? string.Empty;
        }

        if (content.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in content.EnumerateArray())
            {
                if (item.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
                {
                    return text.GetString() ?? string.Empty;
                }
            }
        }

        throw new AiUpstreamException("The AI service response did not contain text.");
    }

    private static CouponImageAnalysisDto ParseAnalysis(string content)
    {
        var json = RemoveMarkdownFence(content);

        AiResponse parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<AiResponse>(json, JsonOptions)
                ?? throw new JsonException("The completion was empty.");
        }
        catch (JsonException ex)
        {
            throw new AiUpstreamException("The AI completion did not match the expected JSON schema.", ex);
        }

        var coupons = new List<CouponImageCandidateDto>();
        foreach (var value in (parsed.Coupons ?? []).Take(10))
        {
            if (ToCandidate(value) is { } candidate)
            {
                coupons.Add(candidate);
            }
        }

        return new CouponImageAnalysisDto(coupons);
    }

    private static string RemoveMarkdownFence(string content)
    {
        var trimmed = content.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            return trimmed;
        }

        var firstLine = trimmed.IndexOf('\n');
        var lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
        return firstLine >= 0 && lastFence > firstLine
            ? trimmed[(firstLine + 1)..lastFence].Trim()
            : trimmed;
    }

    private static CouponImageCandidateDto? ToCandidate(AiCandidate value)
    {
        var name = Trim(value.Name, 200);
        var description = Trim(value.Description, 2000);
        var displayValue = Trim(value.DisplayValue, 4000);
        DateOnly? expiry = DateOnly.TryParseExact(value.ExpiresOn, "yyyy-MM-dd", out var date) ? date : null;
        CouponDisplayType? displayType = Enum.TryParse<CouponDisplayType>(value.DisplayType, true, out var type) ? type : null;
        CouponBarcodeType? barcodeType = Enum.TryParse<CouponBarcodeType>(value.BarcodeType, true, out var barcode) ? barcode : null;
        var uses = value.TotalUses is >= 1 and <= 999 ? value.TotalUses : null;
        if (name is null && description is null && displayValue is null)
        {
            return null;
        }

        return new CouponImageCandidateDto(name, description, expiry, uses, displayType, barcodeType, displayValue);
    }

    private static string? Trim(string? value, int length)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= length ? trimmed : trimmed[..length];
    }

    private sealed class AiResponse
    {
        public List<AiCandidate>? Coupons { get; init; }
    }

    private sealed class AiCandidate
    {
        public string? Name { get; init; }
        public string? Description { get; init; }
        public string? ExpiresOn { get; init; }
        public int? TotalUses { get; init; }
        public string? DisplayType { get; init; }
        public string? BarcodeType { get; init; }
        public string? DisplayValue { get; init; }
    }
}

public sealed class AiConfigurationException : Exception;

public sealed class AiUpstreamException(string message, Exception? innerException = null)
    : Exception(message, innerException);
