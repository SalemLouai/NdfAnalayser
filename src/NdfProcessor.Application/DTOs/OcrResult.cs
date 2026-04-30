using NdfProcessor.Domain.Entities;
using NdfProcessor.Domain.Interfaces;

namespace NdfProcessor.Application.DTOs;

/// <summary>
/// Result from OCR analysis
/// </summary>
public class OcrResult
{
    /// <summary>
    /// Extracted receipt
    /// </summary>
    public Receipt Receipt { get; set; } = new();

    /// <summary>
    /// Bounding box if available
    /// </summary>
    public BoundingBox? BoundingBox { get; set; }

    /// <summary>
    /// Confidence score (0-1)
    /// </summary>
    public float Confidence { get; set; }
}
