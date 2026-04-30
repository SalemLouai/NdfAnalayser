using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NdfProcessor.Domain.Entities;
using NdfProcessor.Domain.Interfaces;
using NdfProcessor.Infrastructure.Configuration;

namespace NdfProcessor.Infrastructure.Services;

/// <summary>
/// Handles local file system operations
/// </summary>
public class LocalFileService : IFileService
{
    private readonly AppSettings _settings;
    private readonly ILogger<LocalFileService> _logger;

    public LocalFileService(IOptions<AppSettings> settings, ILogger<LocalFileService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public Task<List<string>> GetInputFilesAsync()
    {
        if (!Directory.Exists(_settings.Paths.InputFolder))
        {
            _logger.LogWarning("Input folder does not exist: {Path}", _settings.Paths.InputFolder);
            return Task.FromResult(new List<string>());
        }

        var supportedExtensions = _settings.Processing.SupportedImageExtensions
            .Concat(_settings.Processing.SupportedPdfExtensions)
            .ToList();

        var files = Directory.GetFiles(_settings.Paths.InputFolder)
            .Where(f => supportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .ToList();

        return Task.FromResult(files);
    }

    public async Task<string> MoveProcessedFileAsync(
        string sourceFilePath,
        DateTime date,
        decimal totalAmount,
        CancellationToken cancellationToken = default)
    {
        var monthFolder = date.ToString(_settings.Processing.ProcessedSubFolderFormat);
        var targetFolder = Path.Combine(_settings.Paths.ProcessedFolderRoot, monthFolder);

        Directory.CreateDirectory(targetFolder);

        var fileName = GenerateProcessedFileName(date, totalAmount, Path.GetExtension(sourceFilePath));
        var targetPath = Path.Combine(targetFolder, fileName);

        // Handle duplicates by adding suffix
        targetPath = GetUniqueFilePath(targetPath);

        await Task.Run(() => File.Move(sourceFilePath, targetPath), cancellationToken);
        _logger.LogInformation("Moved file: {Source} -> {Target}", sourceFilePath, targetPath);

        return targetPath;
    }

    public async Task MoveToErrorFolderAsync(string sourceFilePath, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_settings.Paths.ErrorFolder);

        var fileName = Path.GetFileName(sourceFilePath);
        var targetPath = Path.Combine(_settings.Paths.ErrorFolder, fileName);

        targetPath = GetUniqueFilePath(targetPath);

        await Task.Run(() => File.Move(sourceFilePath, targetPath), cancellationToken);
        _logger.LogWarning("Moved to error folder: {Path}", targetPath);
    }

    public async Task<string> SaveCroppedReceiptAsync(
        byte[] imageBytes,
        DateTime date,
        decimal totalAmount,
        CancellationToken cancellationToken = default)
    {
        var monthFolder = date.ToString(_settings.Processing.ProcessedSubFolderFormat);
        var targetFolder = Path.Combine(_settings.Paths.ProcessedFolderRoot, monthFolder);

        Directory.CreateDirectory(targetFolder);

        var fileName = GenerateProcessedFileName(date, totalAmount, ".png");
        var targetPath = Path.Combine(targetFolder, fileName);

        targetPath = GetUniqueFilePath(targetPath);

        await File.WriteAllBytesAsync(targetPath, imageBytes, cancellationToken);
        _logger.LogInformation("Saved cropped receipt: {Path}", targetPath);

        return targetPath;
    }

    public bool IsImageFile(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return _settings.Processing.SupportedImageExtensions.Contains(extension);
    }

    public bool IsPdfFile(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return _settings.Processing.SupportedPdfExtensions.Contains(extension);
    }

    public async Task WriteErrorReportAsync(
        string runId,
        List<ProcessingResult> errorResults,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_settings.Paths.ErrorReportFolder);

        var reportPath = Path.Combine(_settings.Paths.ErrorReportFolder, $"ErrorReport_{runId}.txt");

        using var writer = new StreamWriter(reportPath);
        await writer.WriteLineAsync($"Error Report - Run ID: {runId}");
        await writer.WriteLineAsync($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        await writer.WriteLineAsync(new string('-', 80));
        await writer.WriteLineAsync();

        foreach (var result in errorResults)
        {
            await writer.WriteLineAsync($"File: {result.FilePath}");
            await writer.WriteLineAsync($"Error: {result.ErrorMessage}");
            await writer.WriteLineAsync();
        }

        _logger.LogInformation("Error report written: {Path}", reportPath);
    }

    private string GenerateProcessedFileName(DateTime date, decimal totalAmount, string extension)
    {
        var format = _settings.Processing.ProcessedFileNameFormat;

        // Replace {date:format} placeholder
        var datePattern = System.Text.RegularExpressions.Regex.Match(format, @"\{date:([^\}]+)\}");
        if (datePattern.Success)
        {
            var dateFormat = datePattern.Groups[1].Value;
            format = format.Replace(datePattern.Value, date.ToString(dateFormat));
        }

        // Replace {totalAmount} placeholder
        var amountStr = totalAmount.ToString("F2").Replace(".", "_");
        format = format.Replace("{totalAmount}", amountStr);

        return format + extension;
    }

    private static string GetUniqueFilePath(string basePath)
    {
        if (!File.Exists(basePath))
            return basePath;

        var directory = Path.GetDirectoryName(basePath) ?? "";
        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(basePath);
        var extension = Path.GetExtension(basePath);

        var counter = 1;
        string uniquePath;

        do
        {
            uniquePath = Path.Combine(directory, $"{fileNameWithoutExt}_{counter}{extension}");
            counter++;
        } while (File.Exists(uniquePath));

        return uniquePath;
    }
}
