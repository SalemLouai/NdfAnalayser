using Microsoft.Extensions.Logging;
using NdfProcessor.Domain.Entities;
using NdfProcessor.Domain.Enums;
using NdfProcessor.Domain.Interfaces;

namespace NdfProcessor.Application.UseCases;

/// <summary>
/// Main use case for processing receipts
/// </summary>
public class ProcessReceiptsUseCase
{
    private readonly IOcrService _ocrService;
    private readonly IExcelService _excelService;
    private readonly IFileService _fileService;
    private readonly IPdfService _pdfService;
    private readonly IDuplicateDetector _duplicateDetector;
    private readonly ILogger<ProcessReceiptsUseCase> _logger;

    public ProcessReceiptsUseCase(
        IOcrService ocrService,
        IExcelService excelService,
        IFileService fileService,
        IPdfService pdfService,
        IDuplicateDetector duplicateDetector,
        ILogger<ProcessReceiptsUseCase> logger)
    {
        _ocrService = ocrService;
        _excelService = excelService;
        _fileService = fileService;
        _pdfService = pdfService;
        _duplicateDetector = duplicateDetector;
        _logger = logger;
    }

    /// <summary>
    /// Executes the receipt processing pipeline
    /// </summary>
    public async Task<RunSummary> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var summary = RunSummary.Create();
        _logger.LogInformation("Starting processing run: {RunId}", summary.RunId);

        try
        {
            // Load existing receipts for duplicate detection
            var existingReceipts = await _excelService.LoadExistingReceiptsAsync(cancellationToken);
            _duplicateDetector.LoadExistingReceipts(existingReceipts);
            _logger.LogInformation("Loaded {Count} existing receipts for duplicate detection", existingReceipts.Count);

            // Get all input files
            var files = await _fileService.GetInputFilesAsync();
            _logger.LogInformation("Found {Count} files to process", files.Count);

            // Process each file
            foreach (var file in files)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var result = await ProcessFileAsync(file, cancellationToken);
                summary.Results.Add(result);
            }

            // Generate error report if needed
            var errorResults = summary.Results.Where(r => r.Status == ProcessingStatus.Error).ToList();
            if (errorResults.Any())
            {
                await _fileService.WriteErrorReportAsync(summary.RunId, errorResults, cancellationToken);
                _logger.LogWarning("Generated error report with {Count} errors", errorResults.Count);
            }

            // Sort all receipts by date in Excel
            _logger.LogInformation("Sorting all receipts by date...");
            await _excelService.SortReceiptsByDateAsync(DateTime.Now.Year, cancellationToken);

            // Complete summary
            summary.Complete();
            summary.ExcelFilePath = _excelService.GetExcelFilePath(DateTime.Now.Year);

            // Log final summary
            LogSummary(summary);

            return summary;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error during processing run");
            throw;
        }
    }

    private async Task<ProcessingResult> ProcessFileAsync(string filePath, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing file: {FilePath}", filePath);

        try
        {
            List<(Receipt Receipt, byte[]? ImageBytes)> receiptsWithImages;

            if (_fileService.IsImageFile(filePath))
            {
                receiptsWithImages = await ProcessImageFileAsync(filePath, cancellationToken);
            }
            else if (_fileService.IsPdfFile(filePath))
            {
                receiptsWithImages = await ProcessPdfFileAsync(filePath, cancellationToken);
            }
            else
            {
                _logger.LogWarning("Unsupported file type: {FilePath}", filePath);
                return ProcessingResult.Error(filePath, "Unsupported file type");
            }

            if (!receiptsWithImages.Any())
            {
                _logger.LogWarning("No receipts extracted from {FilePath}", filePath);
                await _fileService.MoveToErrorFolderAsync(filePath, cancellationToken);
                return ProcessingResult.Error(filePath, "No receipts detected");
            }

            // Process each extracted receipt
            var processedReceipts = new List<Receipt>();
            string? processedPath = null;

            foreach (var (receipt, imageBytes) in receiptsWithImages)
            {
                // Calculate missing amounts
                receipt.CalculateMissingAmounts();

                // Check for duplicates
                if (_duplicateDetector.IsDuplicate(receipt))
                {
                    _logger.LogWarning("Duplicate detected: {FilePath} - {Date} {Restaurant} ${Amount}",
                        filePath, receipt.Date, receipt.Restaurant, receipt.TotalAmount);
                    return ProcessingResult.Duplicate(filePath, receipt);
                }

                // Save cropped image or move file - set ProcessedFilePath before Excel insertion
                if (imageBytes != null)
                {
                    // Multi-receipt PDF: save cropped image
                    var savedPath = await _fileService.SaveCroppedReceiptAsync(
                        imageBytes, receipt.Date, receipt.TotalAmount, cancellationToken);
                    receipt.ProcessedFilePath = savedPath;
                    _logger.LogInformation("Saved cropped receipt: {Path}", savedPath);
                }
                else if (receiptsWithImages.Count == 1)
                {
                    // Single receipt: move original file
                    processedPath = await _fileService.MoveProcessedFileAsync(
                        filePath, receipt.Date, receipt.TotalAmount, cancellationToken);
                    receipt.ProcessedFilePath = processedPath;
                }

                // Insert into Excel
                await _excelService.InsertReceiptAsync(receipt, cancellationToken);
                _duplicateDetector.AddToCache(receipt);

                processedReceipts.Add(receipt);
            }

            // If we haven't moved the file yet (multi-receipt case), move it now
            if (processedPath == null)
            {
                processedPath = await _fileService.MoveProcessedFileAsync(
                    filePath,
                    receiptsWithImages.First().Receipt.Date,
                    receiptsWithImages.First().Receipt.TotalAmount,
                    cancellationToken);
            }

            _logger.LogInformation("Successfully processed {FilePath} -> {Count} receipts",
                filePath, processedReceipts.Count);

            return ProcessingResult.Success(filePath, processedReceipts, processedPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing file: {FilePath}", filePath);
            await _fileService.MoveToErrorFolderAsync(filePath, cancellationToken);
            return ProcessingResult.Error(filePath, ex.Message);
        }
    }

    private async Task<List<(Receipt Receipt, byte[]? ImageBytes)>> ProcessImageFileAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        using var fileStream = File.OpenRead(filePath);
        var receipts = await _ocrService.AnalyzeReceiptAsync(fileStream, cancellationToken);

        return receipts.Select(r => (r.Receipt, (byte[]?)null)).ToList();
    }

    private async Task<List<(Receipt Receipt, byte[]? ImageBytes)>> ProcessPdfFileAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        var receiptsWithImages = await _pdfService.ProcessPdfAsync(filePath, cancellationToken);
        return receiptsWithImages.Select(r => ((Receipt)r.Receipt, (byte[]?)r.ImageBytes)).ToList();
    }

    private void LogSummary(RunSummary summary)
    {
        _logger.LogInformation("=== Run {RunId} completed ===", summary.RunId);
        _logger.LogInformation("Receipts processed: {Count}", summary.ReceiptsProcessed);
        _logger.LogInformation("Receipts in error: {Count}", summary.ErrorCount);
        _logger.LogInformation("Duplicates detected: {Count}", summary.DuplicateCount);
        _logger.LogInformation("Excel file updated: {Path}", summary.ExcelFilePath);
        _logger.LogInformation("Duration: {Duration}", summary.Duration);
    }
}
