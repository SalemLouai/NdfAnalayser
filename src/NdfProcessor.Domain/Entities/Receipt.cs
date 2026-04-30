namespace NdfProcessor.Domain.Entities;

/// <summary>
/// Represents a restaurant receipt with extracted data
/// </summary>
public class Receipt
{
    /// <summary>
    /// Transaction date
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// Transaction time
    /// </summary>
    public TimeSpan? Time { get; set; }

    /// <summary>
    /// Restaurant name
    /// </summary>
    public string Restaurant { get; set; } = string.Empty;

    /// <summary>
    /// Amount excluding tax
    /// </summary>
    public decimal? AmountExcludingTax { get; set; }

    /// <summary>
    /// Tax amount
    /// </summary>
    public decimal? Tax { get; set; }

    /// <summary>
    /// Total amount including tax
    /// </summary>
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// Source file path
    /// </summary>
    public string? SourceFilePath { get; set; }

    /// <summary>
    /// Processed file path (after moving to Processed folder)
    /// </summary>
    public string? ProcessedFilePath { get; set; }

    /// <summary>
    /// Validates and calculates missing amounts
    /// </summary>
    public void CalculateMissingAmounts()
    {
        // If AmountExcludingTax is missing: calculate from Total - Tax
        if (!AmountExcludingTax.HasValue && Tax.HasValue)
        {
            AmountExcludingTax = TotalAmount - Tax.Value;
        }

        // If Tax is missing: calculate from Total - AmountExcludingTax
        if (!Tax.HasValue && AmountExcludingTax.HasValue)
        {
            Tax = TotalAmount - AmountExcludingTax.Value;
        }
    }

    /// <summary>
    /// Returns true if all required fields are present
    /// </summary>
    public bool IsValid()
    {
        return Date != default
               && !string.IsNullOrWhiteSpace(Restaurant)
               && TotalAmount > 0;
    }
}
