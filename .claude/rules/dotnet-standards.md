---
name: .NET 10 Coding Standards
description: Non-negotiable technical constraints for NdfProcessor
---

# Standards techniques obligatoires

## Logging

✅ Utiliser `ILogger<T>` de `Microsoft.Extensions.Logging`
❌ **JAMAIS** de `Console.WriteLine` brut

```csharp
// ✅ GOOD
_logger.LogInformation("Processing file: {FileName}", fileName);
_logger.LogWarning("Duplicate detected: {Receipt}", receipt);
_logger.LogError(ex, "OCR error for file: {FileName}", fileName);

// ❌ BAD
Console.WriteLine($"Processing {fileName}");
```

## Async/Await

✅ **TOUJOURS** async/await pour les opérations I/O
- Appels API (Azure Document Intelligence)
- Lecture/écriture fichiers
- Accès Excel
- Accès réseau

```csharp
// ✅ BON
public async Task<OcrResult> AnalyzeReceiptAsync(Stream imageStream)

// ❌ MAUVAIS  
public OcrResult AnalyzeReceipt(Stream imageStream) // bloquant
```

## Nullable Reference Types

✅ Activé dans tous les projets (`<Nullable>enable</Nullable>`)
✅ Utiliser `?` pour les types nullable
✅ Utiliser `!` uniquement si certitude absolue

## No Magic Strings

❌ **NEVER** hardcode strings in code
✅ Use configuration (`appsettings.json`)
✅ Use constants if necessary

```csharp
// ✅ GOOD
private const string DateFormat = "yyyy-MM-dd";
var columnDate = _config.Excel.Columns.Date;

// ❌ BAD
var date = DateTime.Now.ToString("yyyy-MM-dd");
worksheet.Cell("A2").Value = receipt.Date;
```

## Dependency Injection

✅ **ALWAYS** via constructor
✅ Use `Microsoft.Extensions.DependencyInjection`
❌ **NEVER** use `new` for services

```csharp
// ✅ GOOD
public class ProcessReceiptsUseCase
{
    private readonly IOcrService _ocrService;
    private readonly ILogger<ProcessReceiptsUseCase> _logger;
    
    public ProcessReceiptsUseCase(IOcrService ocrService, ILogger<ProcessReceiptsUseCase> logger)
    {
        _ocrService = ocrService;
        _logger = logger;
    }
}

// ❌ BAD
var ocrService = new AzureOcrService(); // tight coupling
```

## Using Statements

✅ All necessary `using` statements at the top of the file
✅ Remove unused `using` statements (follow IDE suggestions)

## Compilation

✅ Code must compile **without errors**
✅ Code must compile **without warnings**
✅ Use NuGet versions **compatible with .NET 10**
