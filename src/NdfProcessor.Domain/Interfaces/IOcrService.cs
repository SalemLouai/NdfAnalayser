using NdfProcessor.Domain.Entities;

namespace NdfProcessor.Domain.Interfaces;

/// <summary>
/// Service for OCR extraction using Azure Document Intelligence
/// </summary>
public interface IOcrService
{
    /// <summary>
    /// Analyzes an image and extracts receipts
    /// </summary>
    /// <param name="imageStream">Image stream to analyze</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of extracted receipts with bounding boxes</returns>
    Task<List<(Receipt Receipt, BoundingBox? BoundingBox)>> AnalyzeReceiptAsync(
        Stream imageStream,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a bounding box for a detected receipt
/// </summary>
public record BoundingBox(
    double X,
    double Y,
    double Width,
    double Height
);
