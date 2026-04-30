using NdfProcessor.Domain.Entities;
using NdfProcessor.Domain.Interfaces;

namespace NdfProcessor.Infrastructure.Services;

/// <summary>
/// Detects duplicate receipts based on date, time, amount, and restaurant
/// </summary>
public class DuplicateDetector : IDuplicateDetector
{
    private readonly HashSet<string> _receiptHashes = new();

    public void LoadExistingReceipts(List<Receipt> existingReceipts)
    {
        _receiptHashes.Clear();
        foreach (var receipt in existingReceipts)
        {
            _receiptHashes.Add(GenerateHash(receipt));
        }
    }

    public bool IsDuplicate(Receipt receipt)
    {
        var hash = GenerateHash(receipt);
        return _receiptHashes.Contains(hash);
    }

    public void AddToCache(Receipt receipt)
    {
        _receiptHashes.Add(GenerateHash(receipt));
    }

    private static string GenerateHash(Receipt receipt)
    {
        var date = receipt.Date.ToString("yyyy-MM-dd");
        var time = receipt.Time?.ToString(@"hh\:mm") ?? "00:00";
        var restaurant = receipt.Restaurant.Trim().ToLowerInvariant();
        var amount = receipt.TotalAmount.ToString("F2");

        return $"{date}|{time}|{restaurant}|{amount}";
    }
}
