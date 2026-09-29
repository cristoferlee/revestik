using System.ComponentModel.DataAnnotations;
using Revestik.Shared.Inventory;

namespace Revestik.Api.Tests.Inventory;

public sealed class InventoryAdjustmentRequestValidationTests
{
    [Theory]
    [InlineData(InventoryAdjustmentReason.PhysicalCount)]
    [InlineData(InventoryAdjustmentReason.Damage)]
    [InlineData(InventoryAdjustmentReason.Loss)]
    [InlineData(InventoryAdjustmentReason.Theft)]
    [InlineData(InventoryAdjustmentReason.InternalUse)]
    [InlineData(InventoryAdjustmentReason.RegistrationError)]
    [InlineData(InventoryAdjustmentReason.Other)]
    public void Validate_WithSupportedReason_IsValid(
        InventoryAdjustmentReason reason)
    {
        var request = new InventoryAdjustmentRequest
        {
            NewStockQuantity = 10m,
            Reason = reason,
            Notes = "Ajuste válido."
        };

        Assert.Empty(Validate(request));
    }

    [Fact]
    public void Validate_WithWhitespaceNotes_IsInvalid()
    {
        var request = new InventoryAdjustmentRequest
        {
            NewStockQuantity = 10m,
            Reason = InventoryAdjustmentReason.Other,
            Notes = "   "
        };

        Assert.Contains(
            Validate(request),
            result => result.MemberNames.Contains(nameof(request.Notes)));
    }

    [Fact]
    public void Validate_WithUnknownReason_IsInvalid()
    {
        var request = new InventoryAdjustmentRequest
        {
            NewStockQuantity = 10m,
            Reason = (InventoryAdjustmentReason)999,
            Notes = "Motivo inválido."
        };

        Assert.Contains(
            Validate(request),
            result => result.MemberNames.Contains(nameof(request.Reason)));
    }

    private static IReadOnlyList<ValidationResult> Validate(
        InventoryAdjustmentRequest request)
    {
        var results = new List<ValidationResult>();

        Validator.TryValidateObject(
            request,
            new ValidationContext(request),
            results,
            validateAllProperties: true);

        return results;
    }
}