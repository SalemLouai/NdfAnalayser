using NdfProcessor.Domain.Entities;

namespace NdfProcessor.Domain.Interfaces;

/// <summary>
/// Service for detecting duplicate receipts
/// </summary>
public interface IDuplicateDetector
{
    /// <summary>
    /// Loads existing receipts into cache for comparison
    /// </summary>
    /// <param name="existingReceipts">Existing receipts</param>
    void LoadExistingReceipts(List<Receipt> existingReceipts);

    /// <summary>
    /// Checks if a receipt is a duplicate
    /// </summary>
    /// <param name="receipt">Receipt to check</param>
    /// <returns>True if duplicate</returns>
    bool IsDuplicate(Receipt receipt);

    /// <summary>
    /// Adds a receipt to the cache
    /// </summary>
    /// <param name="receipt">Receipt to add</param>
    void AddToCache(Receipt receipt);
}
