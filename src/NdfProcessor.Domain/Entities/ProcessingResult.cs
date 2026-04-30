using NdfProcessor.Domain.Enums;

namespace NdfProcessor.Domain.Entities;

/// <summary>
/// Result of processing a single file
/// </summary>
public class ProcessingResult
{
    /// <summary>
    /// Source file path
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Processing status
    /// </summary>
    public ProcessingStatus Status { get; set; }

    /// <summary>
    /// Receipts extracted from this file
    /// </summary>
    public List<Receipt> Receipts { get; set; } = new();

    /// <summary>
    /// Error message if processing failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Processed file path (after renaming and moving)
    /// </summary>
    public string? ProcessedFilePath { get; set; }

    /// <summary>
    /// Creates a successful result
    /// </summary>
    public static ProcessingResult Success(string filePath, List<Receipt> receipts, string? processedFilePath = null)
    {
        return new ProcessingResult
        {
            FilePath = filePath,
            Status = ProcessingStatus.Success,
            Receipts = receipts,
            ProcessedFilePath = processedFilePath
        };
    }

    /// <summary>
    /// Creates an error result
    /// </summary>
    public static ProcessingResult Error(string filePath, string errorMessage)
    {
        return new ProcessingResult
        {
            FilePath = filePath,
            Status = ProcessingStatus.Error,
            ErrorMessage = errorMessage
        };
    }

    /// <summary>
    /// Creates a duplicate result
    /// </summary>
    public static ProcessingResult Duplicate(string filePath, Receipt receipt)
    {
        return new ProcessingResult
        {
            FilePath = filePath,
            Status = ProcessingStatus.Duplicate,
            Receipts = new List<Receipt> { receipt }
        };
    }
}
