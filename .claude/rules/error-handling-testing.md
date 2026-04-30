---
name: Error Handling and Testing
description: Exception handling and testing requirements for NdfProcessor
---

# Error Handling and Testing

## Exception Handling

✅ **ALWAYS** catch specific exceptions first
✅ Log with context before re-throwing
✅ Use custom business exceptions for Domain layer

```csharp
// ✅ GOOD
try
{
    var result = await _ocrService.AnalyzeReceiptAsync(stream);
    return result;
}
catch (Azure.RequestFailedException ex) when (ex.Status == 429)
{
    _logger.LogWarning("Rate limit reached, retrying in {Seconds}s", retryAfter);
    await Task.Delay(retryAfter);
    // Retry logic
}
catch (Azure.RequestFailedException ex)
{
    _logger.LogError(ex, "Azure API error for file: {FileName}", fileName);
    throw new OcrException($"OCR failed for {fileName}", ex);
}
catch (Exception ex)
{
    _logger.LogError(ex, "Unexpected error processing file: {FileName}", fileName);
    throw;
}

// ❌ BAD
try
{
    var result = await _ocrService.AnalyzeReceiptAsync(stream);
}
catch (Exception ex)
{
    // Too generic, no logging, information loss
    throw new Exception("Error");
}
```

## Retry Logic

✅ Implement retry with exponential backoff for Azure calls
✅ Maximum 3 attempts
✅ Log each attempt

```csharp
// ✅ GOOD
private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation, int maxRetries = 3)
{
    for (int attempt = 1; attempt <= maxRetries; attempt++)
    {
        try
        {
            return await operation();
        }
        catch (Exception ex) when (attempt < maxRetries && IsTransient(ex))
        {
            var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
            _logger.LogWarning("Attempt {Attempt}/{MaxRetries} failed, retrying in {Delay}s", 
                attempt, maxRetries, delay.TotalSeconds);
            await Task.Delay(delay);
        }
    }
    throw new InvalidOperationException("Maximum retry attempts reached");
}
```

## Unit Tests

### Required

✅ xUnit + NSubstitute for mocks
✅ Test **all** use cases with mocks
✅ Test Domain logic (calculations, validations)
✅ Name tests in English: `ShouldReturnErrorWhenFileIsInvalid()`

### Test Structure

```csharp
// ✅ GOOD - AAA Pattern (Arrange, Act, Assert)
[Fact]
public async Task ShouldDetectDuplicateWhenReceiptIsIdentical()
{
    // Arrange
    var existingReceipt = new Receipt 
    { 
        Date = new DateTime(2024, 1, 15),
        Restaurant = "Le Bistrot",
        TotalAmountIncludingTax = 25.50m
    };
    var duplicateDetector = new DuplicateDetector();
    duplicateDetector.AddToCache(existingReceipt);
    
    var newReceipt = new Receipt
    {
        Date = new DateTime(2024, 1, 15),
        Restaurant = "Le Bistrot",
        TotalAmountIncludingTax = 25.50m
    };
    
    // Act
    var isDuplicate = duplicateDetector.IsDuplicate(newReceipt);
    
    // Assert
    isDuplicate.Should().BeTrue();
}
```

### Minimum Coverage

✅ Domain: **100%** (critical business logic)
✅ Application: **90%+** (use cases)
✅ Infrastructure: **70%+** (services)

### Integration Tests

✅ Test Infrastructure services with real temporary resources
✅ Excel: temporary file created/cleaned in each test
✅ FileService: temporary folder created/cleaned

```csharp
// ✅ GOOD
public class ClosedXmlExcelServiceIntegrationTests : IDisposable
{
    private readonly string _testFilePath;
    
    public ClosedXmlExcelServiceIntegrationTests()
    {
        _testFilePath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.xlsx");
    }
    
    public void Dispose()
    {
        if (File.Exists(_testFilePath))
            File.Delete(_testFilePath);
    }
}
```
