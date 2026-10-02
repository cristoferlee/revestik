using Microsoft.AspNetCore.Components;
using Revestik.Client.Services.Purchases;
using Revestik.Client.Services.Products;
using Revestik.Client.Services.Suppliers;
using Revestik.Shared.Products;
using Revestik.Shared.Purchases;
using Revestik.Shared.Sales;
using Revestik.Shared.Suppliers;

namespace Revestik.Client.Pages;

public partial class Purchases : IDisposable
{
    private readonly CancellationTokenSource cancellationTokenSource = new();

    private IReadOnlyList<SupplierListItemResponse> supplierResults = [];
    private SupplierListItemResponse? selectedSupplier;
    private string supplierSearch = string.Empty;

    private readonly List<PurchaseLineDraft> lines = [];
    private PurchaseLineDraft? lineEditor;

    private DateOnly purchaseDate =
        DateOnly.FromDateTime(DateTime.Today);

    private PurchaseCurrency currency =
        PurchaseCurrency.CRC;

    private decimal? exchangeRate;

    private PurchasePaymentType paymentType =
        PurchasePaymentType.Cash;

    private int creditTermDays = 30;
    private string notes = string.Empty;

    private bool hasInitialPayment;
    private decimal initialPaymentAmount;
    private PaymentMethod initialPaymentMethod =
        PaymentMethod.BankTransfer;
    private DateOnly initialPaymentDate =
        DateOnly.FromDateTime(DateTime.Today);
    private decimal? initialPaymentExchangeRate;
    private string initialPaymentReference = string.Empty;
    private string initialPaymentNotes = string.Empty;

    private bool isSearchingSuppliers;
    private bool isProductSelectorOpen;
    private bool isNewProductOpen;
    private IReadOnlyList<ProductCategoryResponse> categories = [];
    private IReadOnlyList<UnitOfMeasureResponse> units = [];
    private PurchaseNewProductRequest newProduct = CreateNewProductRequest();
    private bool isConfirmationOpen;
    private bool isSaving;

    private string? errorMessage;
    private string? successMessage;

    [Inject]
    private ISupplierApiService SupplierApiService { get; set; } =
        default!;

    [Inject]
    private IPurchaseApiService PurchaseApiService { get; set; } =
        default!;

    [Inject]
    private IInventoryCatalogApiService CatalogApiService { get; set; } =
        default!;

    private static IReadOnlyList<PaymentMethod> PaymentMethods { get; } =
        Enum.GetValues<PaymentMethod>();

    private decimal PurchaseTotal =>
        decimal.Round(
            lines.Sum(line => line.LineTotal),
            2,
            MidpointRounding.AwayFromZero);

    private bool HasInitialPaymentForSubmit =>
        paymentType == PurchasePaymentType.Cash ||
        hasInitialPayment;

    private decimal InitialPaymentForSubmit =>
        paymentType == PurchasePaymentType.Cash
            ? PurchaseTotal
            : initialPaymentAmount;

    protected override async Task OnInitializedAsync()
    {
        await SearchSuppliersAsync();
    }

    private async Task SearchSuppliersAsync()
    {
        isSearchingSuppliers = true;
        errorMessage = null;

        try
        {
            var result =
                await SupplierApiService.GetPageAsync(
                    new SupplierListRequest
                    {
                        Search = string.IsNullOrWhiteSpace(supplierSearch)
                            ? null
                            : supplierSearch.Trim(),
                        IncludeInactive = false,
                        Page = 1,
                        PageSize = 20
                    },
                    cancellationTokenSource.Token);

            supplierResults = result.Items
                .Where(item => item.IsActive)
                .ToList();
        }
        catch (OperationCanceledException)
        {
        }
        catch (HttpRequestException)
        {
            errorMessage =
                "No fue posible cargar los proveedores.";
        }
        finally
        {
            isSearchingSuppliers = false;
        }
    }

    private void SelectSupplier(
        SupplierListItemResponse supplier)
    {
        selectedSupplier = supplier;
        errorMessage = null;
    }

    private void ClearSupplier()
    {
        selectedSupplier = null;
    }

    private async Task OpenNewProductAsync()
    {
        if (lineEditor is not null)
        {
            return;
        }

        errorMessage = null;
        isProductSelectorOpen = false;

        if (categories.Count == 0 || units.Count == 0)
        {
            categories = (await CatalogApiService.GetCategoriesAsync(
                includeInactive: false,
                cancellationTokenSource.Token))
                .Where(item => item.IsActive)
                .OrderBy(item => item.Name)
                .ToList();

            units = (await CatalogApiService.GetUnitsAsync(
                includeInactive: false,
                cancellationTokenSource.Token))
                .Where(item => item.IsActive)
                .OrderBy(item => item.Name)
                .ToList();
        }

        newProduct = CreateNewProductRequest();
        isNewProductOpen = true;
    }

    private void CloseNewProduct()
    {
        isNewProductOpen = false;
        newProduct = CreateNewProductRequest();
    }

    private void OnNewProductInventoryUnitChanged(ChangeEventArgs args)
    {
        newProduct.InventoryUnitId = int.TryParse(args.Value?.ToString(), out var id) ? id : 0;
        NormalizeNewProductUnits();
    }

    private void OnNewProductCommercialUnitChanged(ChangeEventArgs args)
    {
        newProduct.CommercialUnitId = int.TryParse(args.Value?.ToString(), out var id) ? id : 0;
        NormalizeNewProductUnits();
    }

    private void NormalizeNewProductUnits()
    {
        if (newProduct.InventoryUnitId > 0 &&
            newProduct.InventoryUnitId == newProduct.CommercialUnitId)
        {
            newProduct.CommercialUnitsPerInventoryUnit = 1m;
            newProduct.SalePriceBasis = SalePriceBasis.InventoryUnit;
        }
    }

    private void AddNewProductLine()
    {
        var inventoryUnit = units.SingleOrDefault(unit => unit.Id == newProduct.InventoryUnitId);
        var commercialUnit = units.SingleOrDefault(unit => unit.Id == newProduct.CommercialUnitId);

        if (newProduct.CategoryId <= 0 || inventoryUnit is null || commercialUnit is null ||
            string.IsNullOrWhiteSpace(newProduct.Name) ||
            string.IsNullOrWhiteSpace(newProduct.CabysCode) || newProduct.CabysCode.Length != 13 ||
            !newProduct.CabysCode.All(char.IsDigit) ||
            newProduct.CommercialUnitsPerInventoryUnit <= 0m ||
            newProduct.SalePrice <= 0m)
        {
            errorMessage = "Completa correctamente los datos del producto nuevo.";
            return;
        }

        var requiresWhole = newProduct.RequiresWholeInventoryUnits ||
            inventoryUnit.RequiresWholeQuantity;

        lineEditor = new PurchaseLineDraft
        {
            ProductId = 0,
            NewProduct = new PurchaseNewProductRequest
            {
                CategoryId = newProduct.CategoryId,
                Name = newProduct.Name.Trim(),
                Description = newProduct.Description.Trim(),
                CabysCode = newProduct.CabysCode.Trim(),
                InventoryUnitId = newProduct.InventoryUnitId,
                CommercialUnitId = newProduct.CommercialUnitId,
                CommercialUnitsPerInventoryUnit = newProduct.CommercialUnitsPerInventoryUnit,
                RequiresWholeInventoryUnits = requiresWhole,
                SalePrice = newProduct.SalePrice,
                SalePriceBasis = newProduct.SalePriceBasis,
                TaxRate = newProduct.TaxRate,
                MinimumStock = newProduct.MinimumStock
            },
            ProductName = newProduct.Name.Trim(),
            InventoryUnitSymbol = inventoryUnit.Symbol,
            RequiresWholeUnits = requiresWhole,
            Quantity = 1m,
            UnitCost = 0m
        };

        CloseNewProduct();
        errorMessage = null;
    }

    private void OpenProductSelector()
    {
        if (lineEditor is not null)
        {
            return;
        }

        isProductSelectorOpen = true;
        errorMessage = null;
    }

    private void CloseProductSelector()
    {
        isProductSelectorOpen = false;
    }

    private void SelectProduct(
        ProductListItemResponse product)
    {
        if (lines.Any(line =>
                line.ProductId == product.Id))
        {
            errorMessage =
                "Ese producto ya fue agregado a la compra.";
            isProductSelectorOpen = false;
            return;
        }

        lineEditor = new PurchaseLineDraft
        {
            ProductId = product.Id,
            ProductName = product.Name,
            InventoryUnitSymbol =
                product.InventoryUnitSymbol,
            RequiresWholeUnits =
                product.RequiresWholeInventoryUnits,
            Quantity = 1m,
            UnitCost = product.CurrentCost
        };

        isProductSelectorOpen = false;
        isNewProductOpen = false;
        errorMessage = null;
    }

    private void CancelLineEditor()
    {
        lineEditor = null;
    }

    private void ConfirmLine()
    {
        if (lineEditor is null)
        {
            return;
        }

        if (lineEditor.Quantity <= 0m)
        {
            errorMessage =
                "La cantidad debe ser mayor que cero.";
            return;
        }

        if (lineEditor.RequiresWholeUnits &&
            lineEditor.Quantity !=
            decimal.Truncate(lineEditor.Quantity))
        {
            errorMessage =
                "Este producto requiere unidades enteras.";
            return;
        }

        if (decimal.Round(
                lineEditor.Quantity,
                4,
                MidpointRounding.AwayFromZero) !=
            lineEditor.Quantity)
        {
            errorMessage =
                "La cantidad no puede tener más de 4 decimales.";
            return;
        }

        if (lineEditor.UnitCost <= 0m)
        {
            errorMessage =
                "El costo unitario debe ser mayor que cero.";
            return;
        }

        if (decimal.Round(
                lineEditor.UnitCost,
                2,
                MidpointRounding.AwayFromZero) !=
            lineEditor.UnitCost)
        {
            errorMessage =
                "El costo unitario no puede tener más de 2 decimales.";
            return;
        }

        lines.Add(lineEditor);
        lineEditor = null;
        errorMessage = null;
    }

    private void RemoveLine(
        PurchaseLineDraft line)
    {
        if (isSaving)
        {
            return;
        }

        lines.Remove(line);
    }

    private void OpenConfirmation()
    {
        if (!ValidateBeforeConfirmation())
        {
            return;
        }

        if (paymentType == PurchasePaymentType.Cash)
        {
            initialPaymentAmount = PurchaseTotal;
        }

        if (currency == PurchaseCurrency.USD &&
            HasInitialPaymentForSubmit &&
            !initialPaymentExchangeRate.HasValue)
        {
            initialPaymentExchangeRate = exchangeRate;
        }

        isConfirmationOpen = true;
        errorMessage = null;
    }

    private void CloseConfirmation()
    {
        if (!isSaving)
        {
            isConfirmationOpen = false;
        }
    }

    private bool ValidateBeforeConfirmation()
    {
        errorMessage = null;
        successMessage = null;

        if (selectedSupplier is null)
        {
            errorMessage =
                "Selecciona un proveedor.";
            return false;
        }

        if (lines.Count == 0)
        {
            errorMessage =
                "Agrega al menos un producto.";
            return false;
        }

        if (lineEditor is not null)
        {
            errorMessage =
                "Confirma o cancela la línea que estás editando.";
            return false;
        }

        if (currency == PurchaseCurrency.USD &&
            exchangeRate is null or <= 0m)
        {
            errorMessage =
                "Ingresa el tipo de cambio de la compra.";
            return false;
        }

        if (paymentType == PurchasePaymentType.Credit &&
            creditTermDays <= 0)
        {
            errorMessage =
                "El plazo de crédito debe ser mayor que cero.";
            return false;
        }

        if (paymentType == PurchasePaymentType.Credit &&
            hasInitialPayment &&
            (initialPaymentAmount <= 0m ||
             initialPaymentAmount > PurchaseTotal))
        {
            errorMessage =
                "El adelanto debe ser mayor que cero y no superar el total.";
            return false;
        }

        if (currency == PurchaseCurrency.USD &&
            HasInitialPaymentForSubmit &&
            initialPaymentExchangeRate is null or <= 0m)
        {
            errorMessage =
                "Ingresa el tipo de cambio del pago inicial.";
            return false;
        }

        return true;
    }

    private async Task ConfirmPurchaseAsync()
    {
        if (isSaving ||
            !ValidateBeforeConfirmation())
        {
            return;
        }

        isSaving = true;
        errorMessage = null;
        successMessage = null;

        try
        {
            var request = new PurchaseCreateRequest
            {
                SupplierId = selectedSupplier!.Id,
                PurchaseDate = purchaseDate,
                Currency = currency,
                ExchangeRate =
                    currency == PurchaseCurrency.USD
                        ? exchangeRate
                        : null,
                PaymentType = paymentType,
                CreditTermDays =
                    paymentType == PurchasePaymentType.Credit
                        ? creditTermDays
                        : null,
                Notes = notes.Trim(),
                Lines = lines
                    .Select(line =>
                        new PurchaseLineRequest
                        {
                            ProductId = line.ProductId,
                            NewProduct = line.NewProduct,
                            Quantity = line.Quantity,
                            UnitCost = line.UnitCost
                        })
                    .ToList()
            };

            if (HasInitialPaymentForSubmit)
            {
                request.InitialPayment =
                    new PurchasePaymentRequest
                    {
                        Amount = InitialPaymentForSubmit,
                        PaymentMethod =
                            initialPaymentMethod,
                        PaidAtUtc =
                            ToUtc(initialPaymentDate),
                        ExchangeRate =
                            currency ==
                            PurchaseCurrency.USD
                                ? initialPaymentExchangeRate
                                : null,
                        Reference =
                            initialPaymentReference.Trim(),
                        Notes =
                            initialPaymentNotes.Trim()
                    };
            }

            var created =
                await PurchaseApiService.CreateAsync(
                    request,
                    cancellationTokenSource.Token);

            successMessage =
                $"Compra #{created.Id} registrada correctamente.";

            ResetForm();
        }
        catch (OperationCanceledException)
        {
        }
        catch (HttpRequestException)
        {
            errorMessage =
                "No fue posible registrar la compra. " +
                "Revisa los datos e inténtalo nuevamente.";
        }
        finally
        {
            isSaving = false;
        }
    }

    private void ResetForm()
    {
        selectedSupplier = null;
        lines.Clear();
        lineEditor = null;
        isProductSelectorOpen = false;
        isNewProductOpen = false;
        isConfirmationOpen = false;

        purchaseDate =
            DateOnly.FromDateTime(DateTime.Today);

        currency = PurchaseCurrency.CRC;
        exchangeRate = null;
        paymentType = PurchasePaymentType.Cash;
        creditTermDays = 30;
        notes = string.Empty;

        hasInitialPayment = false;
        initialPaymentAmount = 0m;
        initialPaymentMethod = PaymentMethod.BankTransfer;
        initialPaymentDate =
            DateOnly.FromDateTime(DateTime.Today);
        initialPaymentExchangeRate = null;
        initialPaymentReference = string.Empty;
        initialPaymentNotes = string.Empty;
    }

    private static DateTime ToUtc(
        DateOnly date)
    {
        var local =
            date.ToDateTime(TimeOnly.MinValue);

        return DateTime.SpecifyKind(
                local,
                DateTimeKind.Local)
            .ToUniversalTime();
    }

    private static string GetPaymentTypeLabel(
        PurchasePaymentType type) =>
        type switch
        {
            PurchasePaymentType.Cash =>
                "Contado",
            PurchasePaymentType.Credit =>
                "Crédito",
            _ => type.ToString()
        };

    private static string GetPaymentMethodLabel(
        PaymentMethod method) =>
        method switch
        {
            PaymentMethod.Cash =>
                "Efectivo",
            PaymentMethod.Sinpe =>
                "SINPE",
            PaymentMethod.BankTransfer =>
                "Transferencia bancaria",
            PaymentMethod.InternationalTransfer =>
                "Transferencia internacional",
            PaymentMethod.Card =>
                "Tarjeta",
            PaymentMethod.Other =>
                "Otro",
            _ => method.ToString()
        };

    private static PurchaseNewProductRequest CreateNewProductRequest() =>
        new()
        {
            CommercialUnitsPerInventoryUnit = 1m,
            SalePriceBasis = SalePriceBasis.InventoryUnit,
            TaxRate = 13m
        };

    public void Dispose()
    {
        cancellationTokenSource.Cancel();
        cancellationTokenSource.Dispose();
    }

    private sealed class PurchaseLineDraft
    {
        public int ProductId { get; init; }
        public PurchaseNewProductRequest? NewProduct { get; init; }
        public string ProductName { get; init; } =
            string.Empty;
        public string InventoryUnitSymbol { get; init; } =
            string.Empty;
        public bool RequiresWholeUnits { get; init; }
        public decimal Quantity { get; set; }
        public decimal UnitCost { get; set; }

        public decimal LineTotal =>
            decimal.Round(
                Quantity * UnitCost,
                2,
                MidpointRounding.AwayFromZero);
    }
}