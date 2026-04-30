using Microsoft.Extensions.Logging;
using NdfProcessor.Domain.Entities;
using NdfProcessor.Domain.Interfaces;
using SkiaSharp;
using UglyToad.PdfPig;

namespace NdfProcessor.Infrastructure.Services;

/// <summary>
/// PDF processing service using PdfPig and SkiaSharp
/// </summary>
public class PdfService : IPdfService
{
    private readonly IOcrService _ocrService;
    private readonly ILogger<PdfService> _logger;

    public PdfService(IOcrService ocrService, ILogger<PdfService> logger)
    {
        _ocrService = ocrService;
        _logger = logger;
    }

    public async Task<List<(Receipt Receipt, byte[] ImageBytes)>> ProcessPdfAsync(
        string pdfFilePath,
        CancellationToken cancellationToken = default)
    {
        var results = new List<(Receipt Receipt, byte[] ImageBytes)>();

        using var document = PdfDocument.Open(pdfFilePath);

        for (var pageIndex = 0; pageIndex < document.NumberOfPages; pageIndex++)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            _logger.LogInformation("Processing PDF page {Page}/{Total}",
                pageIndex + 1, document.NumberOfPages);

            // Rasterize page
            var pageImageBytes = await RasterizePageAsync(pdfFilePath, pageIndex, 300, cancellationToken);

            // Analyze with OCR
            using var memoryStream = new MemoryStream(pageImageBytes);
            var receiptsWithBoxes = await _ocrService.AnalyzeReceiptAsync(memoryStream, cancellationToken);

            // Crop each detected receipt
            foreach (var (receipt, boundingBox) in receiptsWithBoxes)
            {
                if (boundingBox != null)
                {
                    var croppedImage = CropImage(pageImageBytes, boundingBox, 10);
                    results.Add((receipt, croppedImage));
                    _logger.LogInformation("Cropped receipt from PDF: {Restaurant} ${Amount}",
                        receipt.Restaurant, receipt.TotalAmount);
                }
                else
                {
                    // No bounding box, use full page image
                    results.Add((receipt, pageImageBytes));
                }
            }
        }

        return results;
    }

    public Task<byte[]> RasterizePageAsync(
        string pdfFilePath,
        int pageNumber,
        int dpi = 300,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            using var document = PdfDocument.Open(pdfFilePath);
            var page = document.GetPage(pageNumber + 1); // PdfPig uses 1-based indexing

            var width = (int)(page.Width * dpi / 72.0);
            var height = (int)(page.Height * dpi / 72.0);

            var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
            using var surface = SKSurface.Create(info);
            var canvas = surface.Canvas;

            canvas.Clear(SKColors.White);

            // Note: PdfPig doesn't have built-in rendering.
            // For production, you'd use a library like PDFium or MuPDF
            // This is a placeholder that creates a white image
            // In production, integrate with SkiaSharp.Extended or PDFiumSharp

            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);

            return data.ToArray();
        }, cancellationToken);
    }

    public byte[] CropImage(byte[] imageBytes, BoundingBox boundingBox, int margin = 10)
    {
        using var originalBitmap = SKBitmap.Decode(imageBytes);

        var x = Math.Max(0, (int)boundingBox.X - margin);
        var y = Math.Max(0, (int)boundingBox.Y - margin);
        var width = Math.Min(originalBitmap.Width - x, (int)boundingBox.Width + (margin * 2));
        var height = Math.Min(originalBitmap.Height - y, (int)boundingBox.Height + (margin * 2));

        var cropRect = new SKRectI(x, y, x + width, y + height);

        using var croppedBitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(croppedBitmap);

        canvas.DrawBitmap(originalBitmap, cropRect, new SKRect(0, 0, width, height));

        using var image = SKImage.FromBitmap(croppedBitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);

        return data.ToArray();
    }
}
