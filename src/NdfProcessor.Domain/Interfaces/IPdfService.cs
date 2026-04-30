using NdfProcessor.Domain.Entities;

namespace NdfProcessor.Domain.Interfaces;

/// <summary>
/// Service for PDF operations (rasterization and cropping)
/// </summary>
public interface IPdfService
{
    /// <summary>
    /// Processes a PDF file and extracts individual receipt images
    /// </summary>
    /// <param name="pdfFilePath">Path to PDF file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of receipt images with their data</returns>
    Task<List<(Receipt Receipt, byte[] ImageBytes)>> ProcessPdfAsync(
        string pdfFilePath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rasterizes a PDF page to an image
    /// </summary>
    /// <param name="pdfFilePath">PDF file path</param>
    /// <param name="pageNumber">Page number (0-based)</param>
    /// <param name="dpi">DPI for rasterization (default 300)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Image bytes</returns>
    Task<byte[]> RasterizePageAsync(
        string pdfFilePath,
        int pageNumber,
        int dpi = 300,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Crops an image based on bounding box
    /// </summary>
    /// <param name="imageBytes">Source image bytes</param>
    /// <param name="boundingBox">Bounding box to crop</param>
    /// <param name="margin">Margin in pixels (default 10)</param>
    /// <returns>Cropped image bytes</returns>
    byte[] CropImage(byte[] imageBytes, BoundingBox boundingBox, int margin = 10);
}
