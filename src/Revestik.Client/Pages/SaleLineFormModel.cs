using Revestik.Shared.Products;
using Revestik.Shared.Sales;

namespace Revestik.Client.Pages;

public sealed class SaleLineFormModel
{
    public int? ProductId { get; set; }
    public string CabysCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;

    // For inventory-linked lines this is always the physical warehouse quantity.
    // Manual lines use it directly as the billable quantity.
    public decimal Quantity { get; set; } = 1m;

    public decimal UnitPrice { get; set; }
    public DiscountType? DiscountType { get; set; }
    public decimal DiscountValue { get; set; }
    public decimal TaxRate { get; set; } = 13m;

    public string InventoryUnitSymbol { get; private set; } = string.Empty;
    public string CommercialUnitSymbol { get; private set; } = string.Empty;
    public decimal CommercialUnitsPerInventoryUnit { get; private set; } = 1m;
    public bool RequiresWholeInventoryUnits { get; private set; }
    public SalePriceBasis SalePriceBasis { get; private set; } =
        SalePriceBasis.InventoryUnit;

    public bool IsLinkedToProduct => ProductId.HasValue;

    // Informational content quantity used by the UI, for example
    // 1 sack = 20 kg. It does not determine the charged quantity.
    public decimal CommercialQuantity =>
        !IsLinkedToProduct
            ? Quantity
            : decimal.Round(
                Quantity * CommercialUnitsPerInventoryUnit,
                4,
                MidpointRounding.AwayFromZero);

    public decimal BillableQuantity =>
        !IsLinkedToProduct
            ? Quantity
            : SalePriceBasis == SalePriceBasis.CommercialUnit
                ? CommercialQuantity
                : Quantity;

    public string BillingUnitSymbol =>
        !IsLinkedToProduct
            ? Unit
            : SalePriceBasis == SalePriceBasis.CommercialUnit
                ? CommercialUnitSymbol
                : InventoryUnitSymbol;

    public decimal GrossAmount =>
        Math.Round(
            BillableQuantity * UnitPrice,
            2,
            MidpointRounding.AwayFromZero);

    public decimal DiscountAmount
    {
        get
        {
            var discount = DiscountType switch
            {
                Revestik.Shared.Sales.DiscountType.Percentage =>
                    GrossAmount * DiscountValue / 100m,
                Revestik.Shared.Sales.DiscountType.FixedAmount =>
                    DiscountValue,
                _ => 0m
            };

            return Math.Round(
                Math.Min(Math.Max(0m, discount), GrossAmount),
                2,
                MidpointRounding.AwayFromZero);
        }
    }

    public decimal AmountAfterDiscount =>
        Math.Max(0m, GrossAmount - DiscountAmount);

    public decimal TaxAmount
    {
        get
        {
            if (TaxRate != 13m)
            {
                return 0m;
            }

            var tax = AmountAfterDiscount - AmountAfterDiscount / 1.13m;
            return Math.Round(tax, 2, MidpointRounding.AwayFromZero);
        }
    }

    public decimal TotalAmount => AmountAfterDiscount;

    public void LinkProduct(ProductListItemResponse product)
    {
        ProductId = product.Id;
        Description = product.Name;
        CabysCode = product.CabysCode;
        UnitPrice = product.SalePrice;
        TaxRate = product.TaxRate;

        InventoryUnitSymbol = product.InventoryUnitSymbol;
        CommercialUnitSymbol = product.CommercialUnitSymbol;
        CommercialUnitsPerInventoryUnit = product.CommercialUnitsPerInventoryUnit;
        RequiresWholeInventoryUnits = product.RequiresWholeInventoryUnits;
        SalePriceBasis = product.SalePriceBasis;
        Unit = InventoryUnitSymbol;

        Quantity = 1m;
    }

    public void RestoreInventoryLink(
        ProductResponse product,
        decimal savedBillableQuantity,
        decimal? savedInventoryQuantity = null,
        string? savedInventoryUnit = null,
        decimal? savedConversion = null)
    {
        var savedBillingUnit = Unit;

        ProductId = product.Id;
        Description = product.Name;
        CabysCode = product.CabysCode;
        UnitPrice = product.SalePrice;
        TaxRate = product.TaxRate;

        InventoryUnitSymbol =
            string.IsNullOrWhiteSpace(savedInventoryUnit)
                ? product.InventoryUnitSymbol
                : savedInventoryUnit;
        CommercialUnitSymbol = product.CommercialUnitSymbol;
        CommercialUnitsPerInventoryUnit =
            savedConversion is > 0m
                ? savedConversion.Value
                : product.CommercialUnitsPerInventoryUnit;
        RequiresWholeInventoryUnits = product.RequiresWholeInventoryUnits;
        SalePriceBasis = savedBillingUnit == product.InventoryUnitSymbol
            ? SalePriceBasis.InventoryUnit
            : savedBillingUnit == product.CommercialUnitSymbol
                ? SalePriceBasis.CommercialUnit
                : product.SalePriceBasis;

        Quantity = savedInventoryQuantity is > 0m
            ? savedInventoryQuantity.Value
            : SalePriceBasis == SalePriceBasis.CommercialUnit &&
              CommercialUnitsPerInventoryUnit > 0m
                ? savedBillableQuantity /
                  CommercialUnitsPerInventoryUnit
                : savedBillableQuantity;

        Unit = InventoryUnitSymbol;
    }

    public void CopyInventoryLinkFrom(SaleLineFormModel source)
    {
        InventoryUnitSymbol = source.InventoryUnitSymbol;
        CommercialUnitSymbol = source.CommercialUnitSymbol;
        CommercialUnitsPerInventoryUnit = source.CommercialUnitsPerInventoryUnit;
        RequiresWholeInventoryUnits = source.RequiresWholeInventoryUnits;
        SalePriceBasis = source.SalePriceBasis;
    }

    public void UnlinkProduct()
    {
        ProductId = null;
        InventoryUnitSymbol = string.Empty;
        CommercialUnitSymbol = string.Empty;
        CommercialUnitsPerInventoryUnit = 1m;
        RequiresWholeInventoryUnits = false;
        SalePriceBasis = SalePriceBasis.InventoryUnit;
    }

    public SaleLineRequest ToRequest()
    {
        return new SaleLineRequest
        {
            ProductId = ProductId,
            CabysCode = CabysCode.Trim(),
            Description = Description.Trim(),
            Unit = IsLinkedToProduct
                ? BillingUnitSymbol
                : Unit.Trim(),
            Quantity = BillableQuantity,
            UnitPrice = UnitPrice,
            DiscountType = DiscountType,
            DiscountValue = DiscountValue,
            TaxRate = TaxRate
        };
    }
}