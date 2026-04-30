using FluentAssertions;
using NdfProcessor.Domain.Entities;
using NdfProcessor.Domain.Enums;

namespace NdfProcessor.Domain.Tests;

public class ProcessingResultTests
{
    [Fact]
    public void Success_ShouldCreateSuccessResult()
    {
        // Arrange
        var filePath = "test.jpg";
        var receipts = new List<Receipt>
        {
            new Receipt { Restaurant = "Test", TotalAmount = 100m }
        };

        // Act
        var result = ProcessingResult.Success(filePath, receipts, "processed.jpg");

        // Assert
        result.Status.Should().Be(ProcessingStatus.Success);
        result.FilePath.Should().Be(filePath);
        result.Receipts.Should().HaveCount(1);
        result.ProcessedFilePath.Should().Be("processed.jpg");
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void Error_ShouldCreateErrorResult()
    {
        // Arrange
        var filePath = "test.jpg";
        var errorMessage = "OCR failed";

        // Act
        var result = ProcessingResult.Error(filePath, errorMessage);

        // Assert
        result.Status.Should().Be(ProcessingStatus.Error);
        result.FilePath.Should().Be(filePath);
        result.ErrorMessage.Should().Be(errorMessage);
        result.Receipts.Should().BeEmpty();
    }

    [Fact]
    public void Duplicate_ShouldCreateDuplicateResult()
    {
        // Arrange
        var filePath = "test.jpg";
        var receipt = new Receipt { Restaurant = "Test", TotalAmount = 100m };

        // Act
        var result = ProcessingResult.Duplicate(filePath, receipt);

        // Assert
        result.Status.Should().Be(ProcessingStatus.Duplicate);
        result.FilePath.Should().Be(filePath);
        result.Receipts.Should().HaveCount(1);
        result.Receipts.First().Should().Be(receipt);
    }
}
