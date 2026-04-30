using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NdfProcessor.Domain.Entities;
using NdfProcessor.Domain.Interfaces;
using NdfProcessor.Infrastructure.Configuration;
using SkiaSharp;

namespace NdfProcessor.Infrastructure.Services;

/// <summary>
/// OCR service using Azure Document Intelligence
/// </summary>
public class AzureOcrService : IOcrService
{
    private readonly DocumentAnalysisClient _client;
    private readonly ILogger<AzureOcrService> _logger;
    private const int MaxRetries = 3;

    public AzureOcrService(IOptions<AppSettings> settings, ILogger<AzureOcrService> logger)
    {
        _logger = logger;
        var config = settings.Value.AzureOcr;

        _client = new DocumentAnalysisClient(
            new Uri(config.Endpoint),
            new AzureKeyCredential(config.ApiKey));
    }

    public async Task<List<(Receipt Receipt, BoundingBox? BoundingBox)>> AnalyzeReceiptAsync(
        Stream imageStream,
        CancellationToken cancellationToken = default)
    {
        // Compress image if too large (max 4MB before sending to Azure)
        const long maxSizeBytes = 4 * 1024 * 1024; // 4MB
        Stream streamToAnalyze = imageStream;

        if (imageStream.Length > maxSizeBytes)
        {
            _logger.LogWarning("Image size ({Size:N0} bytes) exceeds limit, compressing...", imageStream.Length);
            streamToAnalyze = CompressImage(imageStream);
            _logger.LogInformation("Compressed image to {Size:N0} bytes", streamToAnalyze.Length);
        }

        var operation = await ExecuteWithRetryAsync(
            async () => await _client.AnalyzeDocumentAsync(
                WaitUntil.Completed,
                "prebuilt-receipt",
                streamToAnalyze,
                cancellationToken: cancellationToken),
            cancellationToken);

        var result = operation.Value;
        var receipts = new List<(Receipt, BoundingBox?)>();

        foreach (var document in result.Documents)
        {
            var receipt = ExtractReceiptFromDocument(document);
            var boundingBox = ExtractBoundingBox(document);

            receipts.Add((receipt, boundingBox));
        }

        _logger.LogInformation("Extracted {Count} receipts from image", receipts.Count);
        return receipts;
    }

    private Receipt ExtractReceiptFromDocument(AnalyzedDocument document)
    {
        var receipt = new Receipt();

        // Extract merchant name
        if (document.Fields.TryGetValue("MerchantName", out var merchantField))
        {
            receipt.Restaurant = merchantField.Content ?? string.Empty;
        }

        // Extract transaction date
        if (document.Fields.TryGetValue("TransactionDate", out var dateField) && dateField.Value != null)
        {
            receipt.Date = dateField.Value.AsDate().DateTime;
        }

        // Extract transaction time
        if (document.Fields.TryGetValue("TransactionTime", out var timeField) && timeField.Value != null)
        {
            receipt.Time = timeField.Value.AsTime();
        }

        // Extract amounts
        if (document.Fields.TryGetValue("Subtotal", out var subtotalField) && subtotalField.Value != null)
        {
            receipt.AmountExcludingTax = (decimal)subtotalField.Value.AsDouble();
        }

        if (document.Fields.TryGetValue("TotalTax", out var taxField) && taxField.Value != null)
        {
            receipt.Tax = (decimal)taxField.Value.AsDouble();
        }

        if (document.Fields.TryGetValue("Total", out var totalField) && totalField.Value != null)
        {
            receipt.TotalAmount = (decimal)totalField.Value.AsDouble();
        }

        // Calculate missing amounts
        receipt.CalculateMissingAmounts();

        // Log warnings if critical data is missing
        if (!receipt.IsValid())
        {
            _logger.LogWarning("Incomplete receipt data extracted. Restaurant: {Restaurant}, Total: {Total}",
                receipt.Restaurant, receipt.TotalAmount);
        }

        return receipt;
    }

    private static BoundingBox? ExtractBoundingBox(AnalyzedDocument document)
    {
        if (document.BoundingRegions == null || !document.BoundingRegions.Any())
            return null;

        var region = document.BoundingRegions.First();
        var points = region.BoundingPolygon;

        if (points.Count < 2)
            return null;

        // Calculate bounding rectangle from polygon
        var minX = points.Min(p => p.X);
        var minY = points.Min(p => p.Y);
        var maxX = points.Max(p => p.X);
        var maxY = points.Max(p => p.Y);

        return new BoundingBox(
            minX,
            minY,
            maxX - minX,
            maxY - minY
        );
    }

    private async Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> operation,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (RequestFailedException ex) when (attempt < MaxRetries && IsTransientError(ex))
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                _logger.LogWarning("Attempt {Attempt}/{MaxRetries} failed (transient error). Retrying in {Delay}s...",
                    attempt, MaxRetries, delay.TotalSeconds);

                await Task.Delay(delay, cancellationToken);
            }
            catch (RequestFailedException ex)
            {
                _logger.LogError(ex, "Azure Document Intelligence request failed: {Status} - {Message}",
                    ex.Status, ex.Message);
                throw;
            }
        }

        throw new InvalidOperationException($"Operation failed after {MaxRetries} attempts");
    }

    private static bool IsTransientError(RequestFailedException ex)
    {
        return ex.Status == 429 || // Rate limit
               ex.Status == 503 || // Service unavailable
               ex.Status == 504;   // Gateway timeout
    }

    private Stream CompressImage(Stream imageStream)
    {
        const int maxDimension = 2000; // Maximum width or height
        const int quality = 85; // JPEG quality (0-100)

        imageStream.Position = 0;

        using var originalBitmap = SKBitmap.Decode(imageStream);
        if (originalBitmap == null)
        {
            _logger.LogWarning("Failed to decode image for compression, using original");
            imageStream.Position = 0;
            return imageStream;
        }

        // Calculate new dimensions while maintaining aspect ratio
        var scale = Math.Min(
            maxDimension / (float)originalBitmap.Width,
            maxDimension / (float)originalBitmap.Height);

        if (scale >= 1.0f)
        {
            // Image is already small enough
            imageStream.Position = 0;
            return imageStream;
        }

        var newWidth = (int)(originalBitmap.Width * scale);
        var newHeight = (int)(originalBitmap.Height * scale);

        using var resizedBitmap = originalBitmap.Resize(
            new SKImageInfo(newWidth, newHeight),
            SKSamplingOptions.Default);

        if (resizedBitmap == null)
        {
            _logger.LogWarning("Failed to resize image, using original");
            imageStream.Position = 0;
            return imageStream;
        }

        using var image = SKImage.FromBitmap(resizedBitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, quality);

        var compressedStream = new MemoryStream();
        data.SaveTo(compressedStream);
        compressedStream.Position = 0;

        return compressedStream;
    }
}
