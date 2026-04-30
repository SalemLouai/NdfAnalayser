namespace NdfProcessor.Domain.Interfaces;

/// <summary>
/// Service for file system operations
/// </summary>
public interface IFileService
{
    /// <summary>
    /// Gets all files from input folder
    /// </summary>
    /// <returns>List of file paths</returns>
    Task<List<string>> GetInputFilesAsync();

    /// <summary>
    /// Moves a processed file to the appropriate folder
    /// </summary>
    /// <param name="sourceFilePath">Source file path</param>
    /// <param name="date">Receipt date (for folder organization)</param>
    /// <param name="totalAmount">Total amount (for file naming)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>New file path</returns>
    Task<string> MoveProcessedFileAsync(
        string sourceFilePath,
        DateTime date,
        decimal totalAmount,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves a file to error folder
    /// </summary>
    /// <param name="sourceFilePath">Source file path</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task MoveToErrorFolderAsync(
        string sourceFilePath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves an image (cropped receipt) to processed folder
    /// </summary>
    /// <param name="imageBytes">Image bytes</param>
    /// <param name="date">Receipt date</param>
    /// <param name="totalAmount">Total amount</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Saved file path</returns>
    Task<string> SaveCroppedReceiptAsync(
        byte[] imageBytes,
        DateTime date,
        decimal totalAmount,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if file is an image
    /// </summary>
    bool IsImageFile(string filePath);

    /// <summary>
    /// Checks if file is a PDF
    /// </summary>
    bool IsPdfFile(string filePath);

    /// <summary>
    /// Writes error report to file
    /// </summary>
    /// <param name="runId">Run ID</param>
    /// <param name="errorResults">Error results</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task WriteErrorReportAsync(
        string runId,
        List<Entities.ProcessingResult> errorResults,
        CancellationToken cancellationToken = default);
}
