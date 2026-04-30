---
name: Language Conventions
description: English code with French user-facing messages for NdfProcessor
---

# Language Conventions

## Code (C#)

✅ Class names, methods, properties: **ENGLISH** (.NET convention)
✅ XML comments: **ENGLISH**
✅ Inline comments: **ENGLISH**
✅ Variable names: **ENGLISH**

```csharp
// ✅ GOOD
/// <summary>
/// Processes a restaurant receipt and extracts data via OCR.
/// </summary>
/// <param name="filePath">Path to the image or PDF file</param>
/// <returns>The extracted receipt with all data</returns>
public async Task<Receipt> ProcessReceiptAsync(string filePath)
{
    _logger.LogInformation("Processing file: {FilePath}", filePath);
    
    try
    {
        // Process the receipt
        var result = await _ocrService.AnalyzeAsync(filePath);
        return result;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error processing receipt");
        throw new ProcessingException("Failed to process receipt", ex);
    }
}

// ❌ BAD
/// <summary>
/// Traite un ticket de restaurant
/// </summary>
public async Task<Receipt> ProcessReceiptAsync(string cheminFichier) // French param name
```

## User-Facing Messages (Optional French)

You can choose to keep log messages and error messages in French for French users:

```csharp
// Option 1: English everywhere (recommended for international code)
_logger.LogInformation("Processing file: {FilePath}", filePath);

// Option 2: French for end-user logs (if preferred)
_logger.LogInformation("Traitement du fichier : {FilePath}", filePath);
```

## Documentation

✅ README.md: **FRENCH** (end-user documentation)
✅ CLAUDE.md: **FRENCH** (project specification)
✅ Code comments: **ENGLISH**
✅ TODO/FIXME: **ENGLISH**

## Configuration

✅ JSON keys: **ENGLISH** (standard convention)
✅ Error messages: **Your choice** (English recommended for consistency)

```json
{
  "Processing": {
    "SupportedImageExtensions": [".jpg", ".png"],
    "ErrorMessage": "Error during processing"
  }
}
```

## Domain Entities

✅ **ALL properties in ENGLISH**

```csharp
// ✅ GOOD - English names
public class Receipt
{
    public string Restaurant { get; set; }
    public decimal TotalAmountIncludingTax { get; set; }
    public decimal TotalAmountExcludingTax { get; set; }
    public decimal TaxAmount { get; set; }
}

// ❌ BAD - French names
public class Receipt
{
    public decimal MontantTTC { get; set; } // NO
    public decimal MontantHT { get; set; }  // NO
}
```
