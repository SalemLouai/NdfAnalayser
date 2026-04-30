namespace NdfProcessor.Domain.Entities;

/// <summary>
/// Summary of a processing run
/// </summary>
public class RunSummary
{
    /// <summary>
    /// Unique run identifier
    /// </summary>
    public string RunId { get; set; } = string.Empty;

    /// <summary>
    /// Run start time
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// Run end time
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// Number of receipts processed successfully
    /// </summary>
    public int ReceiptsProcessed { get; set; }

    /// <summary>
    /// Number of files with errors
    /// </summary>
    public int ErrorCount { get; set; }

    /// <summary>
    /// Number of duplicates detected
    /// </summary>
    public int DuplicateCount { get; set; }

    /// <summary>
    /// Path to updated Excel file
    /// </summary>
    public string? ExcelFilePath { get; set; }

    /// <summary>
    /// Processing results for all files
    /// </summary>
    public List<ProcessingResult> Results { get; set; } = new();

    /// <summary>
    /// Duration of the run
    /// </summary>
    public TimeSpan? Duration =>
        EndTime.HasValue ? EndTime.Value - StartTime : null;

    /// <summary>
    /// Creates a new run summary
    /// </summary>
    public static RunSummary Create()
    {
        return new RunSummary
        {
            RunId = GenerateRunId(),
            StartTime = DateTime.Now
        };
    }

    /// <summary>
    /// Marks the run as complete
    /// </summary>
    public void Complete()
    {
        EndTime = DateTime.Now;
        ReceiptsProcessed = Results.Count(r => r.Status == Enums.ProcessingStatus.Success);
        ErrorCount = Results.Count(r => r.Status == Enums.ProcessingStatus.Error);
        DuplicateCount = Results.Count(r => r.Status == Enums.ProcessingStatus.Duplicate);
    }

    private static string GenerateRunId()
    {
        return DateTime.Now.ToString("yyyyMMdd_HHmmss");
    }
}
