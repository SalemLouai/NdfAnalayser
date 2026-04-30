using FluentAssertions;
using NdfProcessor.Domain.Entities;

namespace NdfProcessor.Domain.Tests;

public class ReceiptTests
{
    [Fact]
    public void CalculateMissingAmounts_WhenAmountExcludingTaxMissing_ShouldCalculateIt()
    {
        // Arrange
        var receipt = new Receipt
        {
            TotalAmount = 100m,
            Tax = 10m,
            AmountExcludingTax = null
        };

        // Act
        receipt.CalculateMissingAmounts();

        // Assert
        receipt.AmountExcludingTax.Should().Be(90m);
    }

    [Fact]
    public void CalculateMissingAmounts_WhenTaxMissing_ShouldCalculateIt()
    {
        // Arrange
        var receipt = new Receipt
        {
            TotalAmount = 100m,
            AmountExcludingTax = 90m,
            Tax = null
        };

        // Act
        receipt.CalculateMissingAmounts();

        // Assert
        receipt.Tax.Should().Be(10m);
    }

    [Fact]
    public void IsValid_WhenAllRequiredFieldsPresent_ShouldReturnTrue()
    {
        // Arrange
        var receipt = new Receipt
        {
            Date = new DateTime(2024, 1, 15),
            Restaurant = "Test Restaurant",
            TotalAmount = 100m
        };

        // Act
        var result = receipt.IsValid();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsValid_WhenRestaurantMissing_ShouldReturnFalse()
    {
        // Arrange
        var receipt = new Receipt
        {
            Date = new DateTime(2024, 1, 15),
            Restaurant = "",
            TotalAmount = 100m
        };

        // Act
        var result = receipt.IsValid();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsValid_WhenTotalAmountZero_ShouldReturnFalse()
    {
        // Arrange
        var receipt = new Receipt
        {
            Date = new DateTime(2024, 1, 15),
            Restaurant = "Test",
            TotalAmount = 0m
        };

        // Act
        var result = receipt.IsValid();

        // Assert
        result.Should().BeFalse();
    }
}
