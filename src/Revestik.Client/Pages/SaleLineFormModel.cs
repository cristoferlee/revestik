using Revestik.Shared.Products;
using Revestik.Shared.Sales;

namespace Revestik.Client.Pages;

public sealed class SaleLineFormModel
{
    public int? ProductId { get; set; }
    public string CabysCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;

    // Manual line: commercial quantity.
    // Inventory-linked line: physical inventory quantity.
    public decimal Quantity { get; set; } = 1m;

    public decimal UnitPrice { get; set; }
    public DiscountType? DiscountType { get; set; }
    public decimal DiscountValue { get; set; }
    public decimal TaxRate { get; set; } = 13m;

    public string InventoryUnitSymbol { get; private set; } = string.Empty;
    public string CommercialUnitSymbol { get; private set; } = string.Empty;
    public decimal CommercialUnitsPerInventoryUnit { get; private set; } = 1m;
    public bool RequiresWholeInventoryUnits { get; private set; }

    public bool IsLinkedToProduct => ProductId.HasValue;

    public decimal CommercialQuantity =>
        IsLinkedToProduct
            ? Quantity * CommercialUnitsPerInventoryUnit
            : Quantity;

    public decimal GrossAmount =>
        Math.Round(CommercialQuantity * UnitPrice, 2, MidpointRounding.AwayFromZero);

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

    public decimal AmountAfterDiscount => Math.Max(0m, GrossAmount - DiscountAmount);

    public decimal TaxAmount
    {
        get
        {
            if (TaxRate != 13m) return 0m;

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
        Unit = product.CommercialUnitSymbol;
        UnitPrice = product.SalePrice;
        TaxRate = product.TaxRate;

        InventoryUnitSymbol = product.InventoryUnitSymbol;
        CommercialUnitSymbol = product.CommercialUnitSymbol;
        CommercialUnitsPerInventoryUnit = product.CommercialUnitsPerInventoryUnit;
        RequiresWholeInventoryUnits = product.RequiresWholeInventoryUnits;

        Quantity = 1m;
    }

    public void RestoreInventoryLink(ProductResponse product, decimal savedCommercialQuantity)
    {
        ProductId = product.Id;
        Description = product.Name;
        CabysCode = product.CabysCode;
        Unit = product.CommercialUnitSymbol;
        UnitPrice = product.SalePrice;
        TaxRate = product.TaxRate;

        InventoryUnitSymbol = product.InventoryUnitSymbol;
        CommercialUnitSymbol = product.CommercialUnitSymbol;
        CommercialUnitsPerInventoryUnit = product.CommercialUnitsPerInventoryUnit;
        RequiresWholeInventoryUnits = product.RequiresWholeInventoryUnits;

        Quantity = CommercialUnitsPerInventoryUnit <= 0m
            ? savedCommercialQuantity
            : savedCommercialQuantity / CommercialUnitsPerInventoryUnit;
    }

    public void CopyInventoryLinkFrom(SaleLineFormModel source)
    {
        InventoryUnitSymbol = source.InventoryUnitSymbol;
        CommercialUnitSymbol = source.CommercialUnitSymbol;
        CommercialUnitsPerInventoryUnit = source.CommercialUnitsPerInventoryUnit;
        RequiresWholeInventoryUnits = source.RequiresWholeInventoryUnits;
    }

    public void UnlinkProduct()
    {
        ProductId = null;
        InventoryUnitSymbol = string.Empty;
        CommercialUnitSymbol = string.Empty;
        CommercialUnitsPerInventoryUnit = 1m;
        RequiresWholeInventoryUnits = false;
    }

    public SaleLineRequest ToRequest()
    {
        return new SaleLineRequest
        {
            ProductId = ProductId,
            CabysCode = CabysCode.Trim(),
            Description = Description.Trim(),
            Unit = Unit.Trim(),
            Quantity = CommercialQuantity,
            UnitPrice = UnitPrice,
            DiscountType = DiscountType,
            DiscountValue = DiscountValue,
            TaxRate = TaxRate
        };
    }
}