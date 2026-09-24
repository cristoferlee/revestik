using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Revestik.Client.Services.Customers;
using Revestik.Client.Services.Sales;
using Revestik.Shared.Customers;
using Revestik.Shared.Products;
using Revestik.Shared.Sales;

namespace Revestik.Client.Pages;

public partial class Sales : ComponentBase, IDisposable
{
    private readonly CancellationTokenSource cancellationTokenSource = new();

    private int? loadedDraftId;

    [Parameter]
    [SupplyParameterFromQuery(Name = "draftId")]
    public int? DraftId { get; set; }

    private CustomerListItemResponse? selectedCustomer;

    private SaleUpsertRequest formModel = new()
    {
        Currency = Currency.CRC
    };

    private List<SaleLineFormModel> saleLines = [];
    private List<SaleChargeFormModel> saleCharges = [];

    private SaleLineFormModel lineEditor = new();
    private SaleLineFormModel? editingSaleLine;

    private bool isLineEditorOpen = true;
    private bool isProductSearchOpen;
    private bool isSavingSale;

    private SaleResponse? currentSale;

    private bool isDownloadingSalePdf;
    private bool isPostIssuePaymentOpen;
    private bool isPostIssueFullPayment;
    private bool isRegisteringPostIssuePayment;

    private SalePaymentRequest postIssuePaymentRequest =
        CreatePostIssuePaymentRequest();

    private DateTime postIssuePaymentDate = DateTime.Today;

    private int? currentSaleId;
    private string? currentSaleNumber;
    private SaleStatus currentSaleStatus = SaleStatus.Draft;

    private string? errorMessage;
    private string? successMessage;

    private static readonly string[] SaleUnitOptions =
    [
        "Unidad",
        "m²",
        "m",
        "cm",
        "mm",
        "Caja",
        "Paquete",
        "Saco",
        "Pieza",
        "Rollo",
        "kg",
        "g",
        "L",
        "mL",
        "Servicio",
        "Hora",
        "Día"
    ];

    [Inject]
    private ISaleApiService SaleApiService { get; set; } = default!;

    [Inject]
    private ICustomerApiService CustomerApiService { get; set; } = default!;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = default!;

    private bool IsReadOnly =>
        currentSaleStatus is SaleStatus.Issued or SaleStatus.Voided;

    private decimal SaleSubtotal =>
        saleLines.Sum(line => line.GrossAmount);

    private decimal SaleLineDiscountTotal =>
        saleLines.Sum(line => line.DiscountAmount);

    private decimal SaleLinesAfterDiscount =>
        saleLines.Sum(line => line.TotalAmount);

    private decimal SaleGeneralDiscountTotal =>
        CalculateGeneralDiscount();

    private decimal SaleChargeTotal =>
        saleCharges.Sum(charge => charge.Amount);

    private decimal SaleTotal =>
        Math.Round(
            Math.Max(
                0m,
                SaleLinesAfterDiscount -
                SaleGeneralDiscountTotal) +
            SaleChargeTotal,
            2,
            MidpointRounding.AwayFromZero);

    private decimal SaleTaxTotal =>
        CalculateEstimatedTaxAfterGeneralDiscount();

    protected override async Task OnParametersSetAsync()
    {
        if (!DraftId.HasValue ||
            loadedDraftId == DraftId.Value)
        {
            return;
        }

        loadedDraftId = DraftId.Value;
        await LoadDraftAsync(DraftId.Value);
    }

    private async Task LoadDraftAsync(
        int saleId)
    {
        errorMessage = null;
        successMessage = null;

        try
        {
            var sale = await SaleApiService.GetByIdAsync(
                saleId,
                cancellationTokenSource.Token);

            if (sale is null)
            {
                errorMessage =
                    "El borrador de venta ya no existe.";
                return;
            }

            if (sale.Status != SaleStatus.Draft)
            {
                errorMessage =
                    "Solo los borradores pueden abrirse para edición.";
                return;
            }

            var customer =
                await CustomerApiService.GetByIdAsync(
                    sale.CustomerId,
                    cancellationTokenSource.Token);

            if (customer is null)
            {
                errorMessage =
                    "No fue posible cargar el cliente del borrador.";
                return;
            }

            selectedCustomer =
                new CustomerListItemResponse(
                    customer.Id,
                    customer.IdentificationNumber,
                    customer.Name,
                    customer.Email,
                    customer.PhoneNumber,
                    customer.IdentificationType,
                    customer.IsActive);

            formModel = new SaleUpsertRequest
            {
                CustomerId = sale.CustomerId,
                Currency = sale.Currency,
                GeneralDiscountType =
                    sale.GeneralDiscountType,
                GeneralDiscountValue =
                    sale.GeneralDiscountValue,
                Observations = sale.Observations
            };

            saleLines = sale.Lines
                .Select(line => new SaleLineFormModel
                {
                    ProductId = line.ProductId,
                    CabysCode = line.CabysCode,
                    Description = line.Description,
                    Unit = line.Unit,
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice,
                    DiscountType = line.DiscountType,
                    DiscountValue = line.DiscountValue,
                    TaxRate = line.TaxRate
                })
                .ToList();

            saleCharges = sale.Charges
                .Select(charge => new SaleChargeFormModel
                {
                    Type = charge.Type,
                    Description = charge.Description,
                    Amount = charge.Amount
                })
                .ToList();

            lineEditor = new SaleLineFormModel();
            editingSaleLine = null;
            isLineEditorOpen = false;
            isProductSearchOpen = false;

            ApplySavedSale(sale);

            successMessage =
                sale.ReplacesSaleId.HasValue
                    ? "Borrador de reemplazo cargado. Revisa los datos antes de emitirlo."
                    : "Borrador de venta cargado.";
        }
        catch (OperationCanceledException)
        {
        }
        catch (HttpRequestException)
        {
            errorMessage =
                "No fue posible cargar el borrador de venta.";
        }
    }

    private async Task OnSelectedCustomerChanged(
        CustomerListItemResponse? customer)
    {
        if (IsReadOnly)
        {
            return;
        }

        selectedCustomer = customer;
        formModel.CustomerId = customer?.Id ?? 0;
        errorMessage = null;

        await Task.CompletedTask;
    }

    private void StartNewSaleLine()
    {
        if (IsReadOnly)
        {
            return;
        }

        CloseProductSearch();
        lineEditor = new SaleLineFormModel();
        editingSaleLine = null;
        isLineEditorOpen = true;
        errorMessage = null;
        successMessage = null;
    }

    private void ConfirmSaleLine()
    {
        if (!ValidateLineEditor())
        {
            return;
        }

        if (editingSaleLine is null)
        {
            saleLines.Add(lineEditor);
        }
        else
        {
            CopyLineValues(lineEditor, editingSaleLine);
        }

        CloseLineEditor();
    }

    private void CancelLineEditor()
    {
        CloseLineEditor();
    }

    private void CloseLineEditor()
    {
        CloseProductSearch();
        lineEditor = new SaleLineFormModel();
        editingSaleLine = null;
        isLineEditorOpen = false;
        errorMessage = null;
        successMessage = null;
    }

    private void EditSaleLine(
        SaleLineFormModel line)
    {
        if (IsReadOnly || isLineEditorOpen)
        {
            return;
        }

        CloseProductSearch();
        lineEditor = CloneLine(line);
        editingSaleLine = line;
        isLineEditorOpen = true;
        errorMessage = null;
        successMessage = null;
    }

    private void RemoveSaleLine(
        SaleLineFormModel line)
    {
        if (IsReadOnly || isLineEditorOpen)
        {
            return;
        }

        saleLines.Remove(line);
        errorMessage = null;
        successMessage = null;
    }

    private static SaleLineFormModel CloneLine(
        SaleLineFormModel line)
    {
        return new SaleLineFormModel
        {
            ProductId = line.ProductId,
            CabysCode = line.CabysCode,
            Description = line.Description,
            Unit = line.Unit,
            Quantity = line.Quantity,
            UnitPrice = line.UnitPrice,
            DiscountType = line.DiscountType,
            DiscountValue = line.DiscountValue,
            TaxRate = line.TaxRate
        };
    }

    private static void CopyLineValues(
        SaleLineFormModel source,
        SaleLineFormModel target)
    {
        target.ProductId = source.ProductId;
        target.CabysCode = source.CabysCode;
        target.Description = source.Description;
        target.Unit = source.Unit;
        target.Quantity = source.Quantity;
        target.UnitPrice = source.UnitPrice;
        target.DiscountType = source.DiscountType;
        target.DiscountValue = source.DiscountValue;
        target.TaxRate = source.TaxRate;
    }

    private bool ValidateLineEditor()
    {
        errorMessage = null;
        successMessage = null;

        if (string.IsNullOrWhiteSpace(lineEditor.Description))
        {
            errorMessage =
                "Ingresa una descripción para la línea.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(lineEditor.Unit))
        {
            errorMessage =
                "Selecciona una unidad para la línea.";
            return false;
        }

        if (lineEditor.Quantity <= 0m)
        {
            errorMessage =
                "La cantidad debe ser mayor que cero.";
            return false;
        }

        if (lineEditor.UnitPrice <= 0m)
        {
            errorMessage =
                "El precio unitario debe ser mayor que cero.";
            return false;
        }

        if (lineEditor.TaxRate is not 0m and not 13m)
        {
            errorMessage =
                "Selecciona una tarifa de IVA válida.";
            return false;
        }

        if (lineEditor.DiscountType is null &&
            lineEditor.DiscountValue != 0m)
        {
            errorMessage =
                "Selecciona un tipo de descuento para la línea.";
            return false;
        }

        if (lineEditor.DiscountType is not null &&
            lineEditor.DiscountValue <= 0m)
        {
            errorMessage =
                "El descuento de la línea debe ser mayor que cero.";
            return false;
        }

        if (lineEditor.DiscountType == DiscountType.Percentage &&
            lineEditor.DiscountValue > 100m)
        {
            errorMessage =
                "El descuento porcentual no puede superar el 100%.";
            return false;
        }

        if (lineEditor.DiscountType == DiscountType.FixedAmount &&
            lineEditor.DiscountValue > lineEditor.GrossAmount)
        {
            errorMessage =
                "El descuento fijo no puede superar el importe de la línea.";
            return false;
        }

        return true;
    }

    private void OpenProductSearch()
    {
        isProductSearchOpen = true;
        errorMessage = null;
    }

    private void CloseProductSearch()
    {
        isProductSearchOpen = false;
    }

    private void SelectProduct(
        ProductListItemResponse product)
    {
        lineEditor.LinkProduct(product);
        CloseProductSearch();
        errorMessage = null;
    }

    private void UnlinkProduct()
    {
        lineEditor.UnlinkProduct();
        CloseProductSearch();
        errorMessage = null;
    }

    private void AddSaleCharge()
    {
        if (IsReadOnly)
        {
            return;
        }

        saleCharges.Add(new SaleChargeFormModel());
    }

    private void RemoveSaleCharge(
        SaleChargeFormModel charge)
    {
        if (IsReadOnly)
        {
            return;
        }

        saleCharges.Remove(charge);
    }

    private async Task SaveDraftAsync()
    {
        if (isSavingSale || IsReadOnly)
        {
            return;
        }

        if (!ValidateSaleBeforeSave())
        {
            return;
        }

        isSavingSale = true;
        errorMessage = null;
        successMessage = null;

        try
        {
            PrepareSaleRequest();

            SaleResponse? sale;

            if (currentSaleId.HasValue)
            {
                sale = await SaleApiService.UpdateAsync(
                    currentSaleId.Value,
                    formModel,
                    cancellationTokenSource.Token);
            }
            else
            {
                sale = await SaleApiService.CreateAsync(
                    formModel,
                    cancellationTokenSource.Token);
            }

            if (sale is null)
            {
                errorMessage =
                    "La venta ya no existe o no pudo ser encontrada.";
                return;
            }

            ApplySavedSale(sale);

            successMessage =
                "Borrador de venta guardado correctamente.";
        }
        catch (OperationCanceledException)
        {
        }
        catch (HttpRequestException)
        {
            errorMessage =
                "No fue posible guardar la venta. Revisa los datos e inténtalo nuevamente.";
        }
        finally
        {
            isSavingSale = false;
        }
    }

    private async Task IssueSaleAsync()
    {
        if (isSavingSale || IsReadOnly)
        {
            return;
        }

        if (!ValidateSaleBeforeSave())
        {
            return;
        }

        isSavingSale = true;
        errorMessage = null;
        successMessage = null;

        try
        {
            PrepareSaleRequest();

            if (!currentSaleId.HasValue)
            {
                var draft = await SaleApiService.CreateAsync(
                    formModel,
                    cancellationTokenSource.Token);

                ApplySavedSale(draft);
            }

            var sale = await SaleApiService.IssueAsync(
                currentSaleId!.Value,
                formModel,
                cancellationTokenSource.Token);

            if (sale is null)
            {
                errorMessage =
                    "La venta ya no existe o no pudo ser encontrada.";
                return;
            }

            ApplySavedSale(sale);

            successMessage =
                $"Venta {sale.SaleNumber} emitida correctamente.";
        }
        catch (OperationCanceledException)
        {
        }
        catch (HttpRequestException)
        {
            errorMessage =
                currentSaleId.HasValue
                    ? "No fue posible emitir la venta. El borrador se conserva y puedes intentarlo nuevamente."
                    : "No fue posible emitir la venta. Revisa los datos e inténtalo nuevamente.";
        }
        finally
        {
            isSavingSale = false;
        }
    }

    private bool ValidateSaleBeforeSave()
    {
        errorMessage = null;
        successMessage = null;

        if (selectedCustomer is null ||
            formModel.CustomerId <= 0)
        {
            errorMessage =
                "Selecciona un cliente antes de guardar la venta.";
            return false;
        }

        if (isLineEditorOpen)
        {
            errorMessage = saleLines.Count == 0
                ? "Agrega la línea actual al resumen antes de guardar la venta."
                : "Guarda los cambios de la línea actual antes de guardar la venta.";

            return false;
        }

        if (saleLines.Count == 0)
        {
            errorMessage =
                "Agrega al menos una línea a la venta.";
            return false;
        }

        if (!ValidateGeneralDiscount())
        {
            return false;
        }

        if (saleCharges.Any(charge => charge.Amount <= 0m))
        {
            errorMessage =
                "Todos los cargos deben tener un monto mayor que cero.";
            return false;
        }

        if (saleCharges.Any(charge =>
                charge.Type == SaleChargeType.Other &&
                string.IsNullOrWhiteSpace(charge.Description)))
        {
            errorMessage =
                "Los cargos de tipo Otro deben incluir una descripción.";
            return false;
        }

        if (formModel.Observations.Length > 2000)
        {
            errorMessage =
                "Las observaciones no pueden superar los 2000 caracteres.";
            return false;
        }

        return true;
    }

    private bool ValidateGeneralDiscount()
    {
        if (formModel.GeneralDiscountType is null)
        {
            if (formModel.GeneralDiscountValue != 0m)
            {
                errorMessage =
                    "Selecciona un tipo de descuento general.";
                return false;
            }

            return true;
        }

        if (formModel.GeneralDiscountValue <= 0m)
        {
            errorMessage =
                "El descuento general debe ser mayor que cero.";
            return false;
        }

        if (formModel.GeneralDiscountType ==
                DiscountType.Percentage &&
            formModel.GeneralDiscountValue > 100m)
        {
            errorMessage =
                "El descuento general porcentual no puede superar el 100%.";
            return false;
        }

        if (formModel.GeneralDiscountType ==
                DiscountType.FixedAmount &&
            formModel.GeneralDiscountValue >
                SaleLinesAfterDiscount)
        {
            errorMessage =
                "El descuento general fijo no puede superar el total de las líneas.";
            return false;
        }

        return true;
    }

    private void PrepareSaleRequest()
    {
        formModel.Lines = saleLines
            .Select(line => line.ToRequest())
            .ToList();

        formModel.Charges = saleCharges
            .Select(charge => charge.ToRequest())
            .ToList();
    }

    private void ApplySavedSale(
        SaleResponse sale)
    {
        currentSale = sale;
        currentSaleId = sale.Id;
        currentSaleNumber = sale.SaleNumber;
        currentSaleStatus = sale.Status;
    }

    private async Task DownloadCurrentSalePdfAsync()
    {
        if (currentSale is null ||
            currentSale.Status != SaleStatus.Issued ||
            isDownloadingSalePdf)
        {
            return;
        }

        isDownloadingSalePdf = true;
        errorMessage = null;

        try
        {
            var pdf = await SaleApiService.GetPdfAsync(
                currentSale.Id,
                cancellationTokenSource.Token);

            if (pdf is null)
            {
                errorMessage =
                    "La venta ya no existe o no pudo ser encontrada.";
                return;
            }

            var fileName =
                $"{currentSale.SaleNumber}.pdf";

            await using var stream =
                new MemoryStream(pdf);

            using var streamReference =
                new DotNetStreamReference(stream);

            await JSRuntime.InvokeVoidAsync(
                "quotationDownload.downloadFileFromStream",
                cancellationTokenSource.Token,
                fileName,
                streamReference);
        }
        catch (OperationCanceledException)
        {
        }
        catch (HttpRequestException)
        {
            errorMessage =
                "No fue posible descargar el PDF de la venta.";
        }
        catch (JSException)
        {
            errorMessage =
                "El PDF fue generado, pero el navegador no pudo iniciar la descarga.";
        }
        finally
        {
            isDownloadingSalePdf = false;
        }
    }

    private void OpenPostIssuePartialPayment()
    {
        if (!CanRegisterPostIssuePayment())
        {
            return;
        }

        postIssuePaymentRequest =
            CreatePostIssuePaymentRequest();

        postIssuePaymentDate = DateTime.Today;
        isPostIssueFullPayment = false;
        isPostIssuePaymentOpen = true;
        errorMessage = null;
        successMessage = null;
    }

    private void OpenPostIssueFullPayment()
    {
        if (!CanRegisterPostIssuePayment())
        {
            return;
        }

        postIssuePaymentRequest =
            CreatePostIssuePaymentRequest();

        postIssuePaymentRequest.Amount =
            currentSale!.OutstandingAmount;

        postIssuePaymentDate = DateTime.Today;
        isPostIssueFullPayment = true;
        isPostIssuePaymentOpen = true;
        errorMessage = null;
        successMessage = null;
    }

    private bool CanRegisterPostIssuePayment() =>
        currentSale is not null &&
        currentSale.Status == SaleStatus.Issued &&
        currentSale.OutstandingAmount > 0m;

    private void ClosePostIssuePayment()
    {
        if (isRegisteringPostIssuePayment)
        {
            return;
        }

        isPostIssuePaymentOpen = false;
        isPostIssueFullPayment = false;

        postIssuePaymentRequest =
            CreatePostIssuePaymentRequest();

        postIssuePaymentDate = DateTime.Today;
    }

    private async Task RegisterPostIssuePaymentAsync()
    {
        if (currentSale is null ||
            isRegisteringPostIssuePayment)
        {
            return;
        }

        if (postIssuePaymentRequest.Amount <= 0m)
        {
            errorMessage =
                "El monto del pago debe ser mayor que cero.";
            return;
        }

        if (postIssuePaymentRequest.Amount >
            currentSale.OutstandingAmount)
        {
            errorMessage =
                "El pago no puede superar el saldo pendiente.";
            return;
        }

        isRegisteringPostIssuePayment = true;
        errorMessage = null;
        successMessage = null;

        try
        {
            postIssuePaymentRequest.PaidAtUtc =
                DateTime.SpecifyKind(
                        postIssuePaymentDate.Date,
                        DateTimeKind.Local)
                    .ToUniversalTime();

            var updatedSale =
                await SaleApiService.RegisterPaymentAsync(
                    currentSale.Id,
                    postIssuePaymentRequest,
                    cancellationTokenSource.Token);

            if (updatedSale is null)
            {
                errorMessage =
                    "La venta ya no existe o no pudo ser encontrada.";
                return;
            }

            ApplySavedSale(updatedSale);
            isPostIssuePaymentOpen = false;
            isPostIssueFullPayment = false;

            postIssuePaymentRequest =
                CreatePostIssuePaymentRequest();

            successMessage =
                updatedSale.OutstandingAmount == 0m
                    ? "Pago registrado. La venta quedó cancelada."
                    : "Pago parcial registrado correctamente.";
        }
        catch (OperationCanceledException)
        {
        }
        catch (HttpRequestException)
        {
            errorMessage =
                "No fue posible registrar el pago. Revisa los datos e inténtalo nuevamente.";
        }
        finally
        {
            isRegisteringPostIssuePayment = false;
        }
    }

    private static SalePaymentRequest
        CreatePostIssuePaymentRequest() =>
        new()
        {
            PaymentMethod = PaymentMethod.Cash,
            PaidAtUtc = DateTime.UtcNow
        };

    private decimal CalculateGeneralDiscount()
    {
        if (formModel.GeneralDiscountType is null ||
            formModel.GeneralDiscountValue <= 0m ||
            SaleLinesAfterDiscount <= 0m)
        {
            return 0m;
        }

        var discount =
            formModel.GeneralDiscountType ==
                DiscountType.Percentage
                ? SaleLinesAfterDiscount *
                  formModel.GeneralDiscountValue /
                  100m
                : formModel.GeneralDiscountValue;

        return Math.Round(
            Math.Min(
                discount,
                SaleLinesAfterDiscount),
            2,
            MidpointRounding.AwayFromZero);
    }

    private decimal CalculateEstimatedTaxAfterGeneralDiscount()
    {
        if (saleLines.Count == 0 ||
            SaleLinesAfterDiscount <= 0m)
        {
            return 0m;
        }

        var generalDiscount =
            SaleGeneralDiscountTotal;

        var remainingDiscount =
            generalDiscount;

        decimal taxTotal = 0m;

        for (var index = 0;
             index < saleLines.Count;
             index++)
        {
            var line = saleLines[index];
            var lineAmount =
                line.TotalAmount;

            decimal allocatedDiscount;

            if (generalDiscount == 0m)
            {
                allocatedDiscount = 0m;
            }
            else if (index == saleLines.Count - 1)
            {
                allocatedDiscount =
                    remainingDiscount;
            }
            else
            {
                allocatedDiscount =
                    Math.Round(
                        generalDiscount *
                        lineAmount /
                        SaleLinesAfterDiscount,
                        2,
                        MidpointRounding.AwayFromZero);

                allocatedDiscount =
                    Math.Min(
                        allocatedDiscount,
                        remainingDiscount);
            }

            remainingDiscount -= allocatedDiscount;

            var discountedAmount =
                Math.Max(
                    0m,
                    lineAmount -
                    allocatedDiscount);

            if (line.TaxRate == 13m)
            {
                var lineTax =
                    discountedAmount -
                    discountedAmount / 1.13m;

                taxTotal += Math.Round(
                    lineTax,
                    2,
                    MidpointRounding.AwayFromZero);
            }
        }

        return Math.Round(
            taxTotal,
            2,
            MidpointRounding.AwayFromZero);
    }

    private static string GetSaleStatusLabel(
        SaleStatus status) =>
        status switch
        {
            SaleStatus.Draft => "Borrador",
            SaleStatus.Issued => "Emitida",
            SaleStatus.Voided => "Anulada",
            _ => status.ToString()
        };

    public void Dispose()
    {
        cancellationTokenSource.Cancel();
        cancellationTokenSource.Dispose();
    }
}