using NdfProcessor.Domain.Entities;

namespace NdfProcessor.Domain.Interfaces;

/// <summary>
/// Service for Excel file operations
/// </summary>
public interface IExcelService
{
    /// <summary>
    /// Inserts a receipt into the Excel file
    /// </summary>
    /// <param name="receipt">Receipt to insert</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task InsertReceiptAsync(Receipt receipt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads all existing receipts from Excel for duplicate detection
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of existing receipts</returns>
    Task<List<Receipt>> LoadExistingReceiptsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the path to the Excel file for a given year
    /// </summary>
    /// <param name="year">Year</param>
    /// <returns>Excel file path</returns>
    string GetExcelFilePath(int year);

    /// <summary>
    /// Sorts all receipts by date in all month tabs
    /// </summary>
    /// <param name="year">Year of the Excel file to sort</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SortReceiptsByDateAsync(int year, CancellationToken cancellationToken = default);
}
