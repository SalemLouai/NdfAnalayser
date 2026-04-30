namespace NdfProcessor.Domain.Enums;

/// <summary>
/// Status of receipt processing
/// </summary>
public enum ProcessingStatus
{
    /// <summary>
    /// Receipt processed successfully
    /// </summary>
    Success,

    /// <summary>
    /// Error occurred during processing
    /// </summary>
    Error,

    /// <summary>
    /// Duplicate receipt detected
    /// </summary>
    Duplicate
}
