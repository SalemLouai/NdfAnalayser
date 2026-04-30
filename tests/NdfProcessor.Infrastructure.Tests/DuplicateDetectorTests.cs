using FluentAssertions;
using NdfProcessor.Domain.Entities;
using NdfProcessor.Infrastructure.Services;

namespace NdfProcessor.Infrastructure.Tests;

public class DuplicateDetectorTests
{
    [Fact]
    public void IsDuplicate_WhenReceiptIsNew_ShouldReturnFalse()
    {
        // Arrange
        var detector = new DuplicateDetector();
        var receipt = new Receipt
        {
            Date = new DateTime(2024, 1, 15),
            Time = new TimeSpan(12, 30, 0),
            Restaurant = "Test Restaurant",
            TotalAmount = 25.50m
        };

        // Act
        var result = detector.IsDuplicate(receipt);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsDuplicate_WhenReceiptIsIdentical_ShouldReturnTrue()
    {
        // Arrange
        var detector = new DuplicateDetector();
        var receipt1 = new Receipt
        {
            Date = new DateTime(2024, 1, 15),
            Time = new TimeSpan(12, 30, 0),
            Restaurant = "Test Restaurant",
            TotalAmount = 25.50m
        };
        var receipt2 = new Receipt
        {
            Date = new DateTime(2024, 1, 15),
            Time = new TimeSpan(12, 30, 0),
            Restaurant = "Test Restaurant",
            TotalAmount = 25.50m
        };

        detector.AddToCache(receipt1);

        // Act
        var result = detector.IsDuplicate(receipt2);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsDuplicate_WhenRestaurantNameDifferentCase_ShouldReturnTrue()
    {
        // Arrange
        var detector = new DuplicateDetector();
        var receipt1 = new Receipt
        {
            Date = new DateTime(2024, 1, 15),
            Time = new TimeSpan(12, 30, 0),
            Restaurant = "TEST RESTAURANT",
            TotalAmount = 25.50m
        };
        var receipt2 = new Receipt
        {
            Date = new DateTime(2024, 1, 15),
            Time = new TimeSpan(12, 30, 0),
            Restaurant = "test restaurant",
            TotalAmount = 25.50m
        };

        detector.AddToCache(receipt1);

        // Act
        var result = detector.IsDuplicate(receipt2);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsDuplicate_WhenAmountDifferent_ShouldReturnFalse()
    {
        // Arrange
        var detector = new DuplicateDetector();
        var receipt1 = new Receipt
        {
            Date = new DateTime(2024, 1, 15),
            Time = new TimeSpan(12, 30, 0),
            Restaurant = "Test Restaurant",
            TotalAmount = 25.50m
        };
        var receipt2 = new Receipt
        {
            Date = new DateTime(2024, 1, 15),
            Time = new TimeSpan(12, 30, 0),
            Restaurant = "Test Restaurant",
            TotalAmount = 26.00m
        };

        detector.AddToCache(receipt1);

        // Act
        var result = detector.IsDuplicate(receipt2);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void LoadExistingReceipts_ShouldPopulateCache()
    {
        // Arrange
        var detector = new DuplicateDetector();
        var existingReceipts = new List<Receipt>
        {
            new Receipt
            {
                Date = new DateTime(2024, 1, 15),
                Time = new TimeSpan(12, 30, 0),
                Restaurant = "Restaurant 1",
                TotalAmount = 25.50m
            },
            new Receipt
            {
                Date = new DateTime(2024, 1, 16),
                Time = new TimeSpan(18, 45, 0),
                Restaurant = "Restaurant 2",
                TotalAmount = 32.00m
            }
        };

        // Act
        detector.LoadExistingReceipts(existingReceipts);

        // Assert
        detector.IsDuplicate(existingReceipts[0]).Should().BeTrue();
        detector.IsDuplicate(existingReceipts[1]).Should().BeTrue();
    }
}
