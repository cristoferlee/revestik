using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Revestik.Client.Services.Customers;
using Revestik.Client.Services.Locations;
using Revestik.Client.Services.Products;
using Revestik.Client.Services.Quotations;
using Revestik.Client.Services.Taxpayers;
using Revestik.Shared.Customers;
using Revestik.Shared.Locations;
using Revestik.Shared.Products;
using Revestik.Shared.Quotations;
using Revestik.Shared.Taxpayers;

namespace Revestik.Client.Pages;

public partial class Quotes : ComponentBase, IDisposable
{
    private readonly CancellationTokenSource cancellationTokenSource = new();

    private IReadOnlyList<CustomerListItemResponse>
        customerSearchResults = [];

    private IReadOnlyList<ProductListItemResponse>
        productSearchResults = [];

    private IReadOnlyList<LocationOptionResponse> newCustomerProvinces = [];
    private IReadOnlyList<LocationOptionResponse> newCustomerCantons = [];
    private IReadOnlyList<LocationOptionResponse> newCustomerDistricts = [];

    private CustomerListItemResponse? selectedCustomer;

    private QuotationLineFormModel? selectedProductLine;

    private CustomerUpsertRequest newCustomerModel = CreateNewCustomerModel();

    private QuotationUpsertRequest formModel = new()
    {
        Currency = Currency.CRC,
        ValidUntilUtc = DateTime.Today.AddDays(30)
    };

    private List<QuotationLineFormModel> quotationLines = [];

    private QuotationLineFormModel lineEditor = new();
    private QuotationLineFormModel? editingQuotationLine;
    private bool isLineEditorOpen = true;

    private static readonly string[] QuotationUnitOptions =
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

    private List<QuotationChargeFormModel> quotationCharges = [];

    private string customerSearchTerm = string.Empty;
    private string productSearchTerm = string.Empty;
    private string? errorMessage;
    private string? newCustomerErrorMessage;
    private string? newCustomerTaxpayerMessage;
    private string? newCustomerLocationErrorMessage;

    private bool isSearchingCustomers;
    private bool hasSearchedCustomers;
    private bool isSearchingProducts;
    private bool hasSearchedProducts;
    private bool isNewCustomerFormOpen;
    private bool isSavingNewCustomer;
    private bool isLookingUpNewCustomerTaxpayer;
    private bool isLoadingNewCustomerLocations;
    private bool wasNewCustomerTaxpayerLookupSuccessful;
    private bool isSavingQuotation;
    private bool isDownloadingQuotationPdf;

    private int? currentQuotationId;
    private string? currentQuotationNumber;
    private QuotationStatus currentQuotationStatus = QuotationStatus.Draft;
    private string? quotationSuccessMessage;

    [Inject]
    private ICustomerApiService CustomerApiService { get; set; } = default!;

    [Inject]
    private IProductApiService ProductApiService { get; set; } = default!;

    [Inject]
    private ITaxpayerApiService TaxpayerApiService { get; set; } = default!;

    [Inject]
    private ILocationApiService LocationApiService { get; set; } = default!;

    [Inject]
    private IQuotationApiService QuotationApiService { get; set; } = default!;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = default!;

    private decimal QuotationSubtotal =>
        quotationLines.Sum(line => line.GrossAmount);

    private decimal QuotationDiscountTotal =>
        quotationLines.Sum(line => line.DiscountAmount);

    private decimal QuotationTaxTotal =>
        quotationLines.Sum(line => line.TaxAmount);

    private decimal QuotationChargeTotal =>
        quotationCharges.Sum(charge => charge.Amount);

    private decimal QuotationTotal =>
        quotationLines.Sum(line => line.TotalAmount) +
        QuotationChargeTotal;

    private string NewCustomerIdentificationRule =>
        newCustomerModel.IdentificationType switch
        {
            IdentificationType.PhysicalPerson =>
                "Debe contener 9 dígitos, sin guiones y sin cero al inicio.",

            IdentificationType.Dimex =>
                "Debe contener 11 o 12 dígitos, sin guiones y sin cero al inicio.",

            IdentificationType.LegalEntity =>
                "Debe contener 10 dígitos y sin guiones.",

            _ => string.Empty
        };

    private string NewCustomerIdentificationPlaceholder =>
        newCustomerModel.IdentificationType switch
        {
            IdentificationType.PhysicalPerson =>
                "Ejemplo: 109870654",

            IdentificationType.Dimex =>
                "Ejemplo: 12345678901",

            IdentificationType.LegalEntity =>
                "Ejemplo: 3101234567",

            _ =>
                "Seleccione primero el tipo de identificación"
        };

    private int NewCustomerIdentificationMaximumLength =>
        newCustomerModel.IdentificationType switch
        {
            IdentificationType.PhysicalPerson => 9,
            IdentificationType.Dimex => 12,
            IdentificationType.LegalEntity => 10,
            _ => 12
        };

    private bool CanLookupNewCustomerTaxpayer =>
        newCustomerModel.IdentificationType switch
        {
            IdentificationType.PhysicalPerson =>
                IsValidNewCustomerIdentification(9),

            IdentificationType.Dimex =>
                IsValidNewCustomerIdentification(11) ||
                IsValidNewCustomerIdentification(12),

            IdentificationType.LegalEntity =>
                IsValidNewCustomerIdentification(10),

            _ => false
        };

    private bool CanSelectNewCustomerCanton =>
        !isLoadingNewCustomerLocations &&
        !string.IsNullOrWhiteSpace(newCustomerModel.ProvinceCode);

    private bool CanSelectNewCustomerDistrict =>
        !isLoadingNewCustomerLocations &&
        !string.IsNullOrWhiteSpace(newCustomerModel.ProvinceCode) &&
        !string.IsNullOrWhiteSpace(newCustomerModel.CantonCode);

    private bool HasCompleteNewCustomerLocation =>
        !string.IsNullOrWhiteSpace(newCustomerModel.ProvinceCode) &&
        !string.IsNullOrWhiteSpace(newCustomerModel.CantonCode) &&
        !string.IsNullOrWhiteSpace(newCustomerModel.DistrictCode) &&
        !string.IsNullOrWhiteSpace(newCustomerModel.OtherSigns) &&
        newCustomerModel.OtherSigns.Trim().Length is >= 5 and <= 160;

    private async Task SearchCustomersAsync()
    {
        errorMessage = null;
        hasSearchedCustomers = true;

        if (string.IsNullOrWhiteSpace(customerSearchTerm))
        {
            customerSearchResults = [];
            return;
        }

        isSearchingCustomers = true;

        try
        {
            var result = await CustomerApiService.GetPageAsync(
                new CustomerListRequest
                {
                    Search = customerSearchTerm.Trim(),
                    Page = 1,
                    PageSize = 20
                },
                cancellationTokenSource.Token);

            customerSearchResults = result.Items
                .Where(customer => customer.IsActive)
                .ToList();
        }
        catch (OperationCanceledException)
        {
        }
        catch (HttpRequestException)
        {
            errorMessage =
                "No fue posible buscar clientes. Verifica la conexión con la API.";
        }
        finally
        {
            isSearchingCustomers = false;
        }
    }

    private void SelectCustomer(
        CustomerListItemResponse customer)
    {
        selectedCustomer = customer;
        formModel.CustomerId = customer.Id;

        customerSearchResults = [];
        customerSearchTerm = string.Empty;
        hasSearchedCustomers = false;
        errorMessage = null;

        CloseNewCustomerForm();
    }

    private void ClearSelectedCustomer()
    {
        selectedCustomer = null;
        formModel.CustomerId = 0;

        customerSearchResults = [];
        customerSearchTerm = string.Empty;
        hasSearchedCustomers = false;
        errorMessage = null;
    }

    private void ClearCustomerSearch()
    {
        customerSearchTerm = string.Empty;
        customerSearchResults = [];
        hasSearchedCustomers = false;
        errorMessage = null;
    }

    private async Task OpenNewCustomerFormAsync()
    {
        isNewCustomerFormOpen = true;
        newCustomerModel = CreateNewCustomerModel();
        newCustomerProvinces = [];
        newCustomerCantons = [];
        newCustomerDistricts = [];
        newCustomerErrorMessage = null;
        newCustomerTaxpayerMessage = null;
        newCustomerLocationErrorMessage = null;
        wasNewCustomerTaxpayerLookupSuccessful = false;

        await LoadNewCustomerProvincesAsync();
    }

    private void CloseNewCustomerForm()
    {
        isNewCustomerFormOpen = false;
        newCustomerModel = CreateNewCustomerModel();
        newCustomerProvinces = [];
        newCustomerCantons = [];
        newCustomerDistricts = [];
        newCustomerErrorMessage = null;
        newCustomerTaxpayerMessage = null;
        newCustomerLocationErrorMessage = null;
        wasNewCustomerTaxpayerLookupSuccessful = false;
    }

    private async Task LoadNewCustomerProvincesAsync()
    {
        isLoadingNewCustomerLocations = true;
        newCustomerLocationErrorMessage = null;

        try
        {
            newCustomerProvinces =
                await LocationApiService.GetProvincesAsync(
                    cancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (HttpRequestException)
        {
            newCustomerLocationErrorMessage =
                "No fue posible cargar las provincias. Inténtalo nuevamente.";
        }
        finally
        {
            isLoadingNewCustomerLocations = false;
        }
    }

    private async Task OnNewCustomerProvinceChangedAsync(
        ChangeEventArgs eventArgs)
    {
        newCustomerModel.ProvinceCode =
            eventArgs.Value?.ToString() ?? string.Empty;

        newCustomerModel.CantonCode = string.Empty;
        newCustomerModel.DistrictCode = string.Empty;

        newCustomerCantons = [];
        newCustomerDistricts = [];
        newCustomerLocationErrorMessage = null;

        if (string.IsNullOrWhiteSpace(
                newCustomerModel.ProvinceCode))
        {
            return;
        }

        isLoadingNewCustomerLocations = true;

        try
        {
            newCustomerCantons =
                await LocationApiService.GetCantonsAsync(
                    newCustomerModel.ProvinceCode,
                    cancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (HttpRequestException)
        {
            newCustomerLocationErrorMessage =
                "No fue posible cargar los cantones. Inténtalo nuevamente.";
        }
        finally
        {
            isLoadingNewCustomerLocations = false;
        }
    }

    private async Task OnNewCustomerCantonChangedAsync(
        ChangeEventArgs eventArgs)
    {
        newCustomerModel.CantonCode =
            eventArgs.Value?.ToString() ?? string.Empty;

        newCustomerModel.DistrictCode = string.Empty;

        newCustomerDistricts = [];
        newCustomerLocationErrorMessage = null;

        if (string.IsNullOrWhiteSpace(
                newCustomerModel.ProvinceCode) ||
            string.IsNullOrWhiteSpace(
                newCustomerModel.CantonCode))
        {
            return;
        }

        isLoadingNewCustomerLocations = true;

        try
        {
            newCustomerDistricts =
                await LocationApiService.GetDistrictsAsync(
                    newCustomerModel.ProvinceCode,
                    newCustomerModel.CantonCode,
                    cancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (HttpRequestException)
        {
            newCustomerLocationErrorMessage =
                "No fue posible cargar los distritos. Inténtalo nuevamente.";
        }
        finally
        {
            isLoadingNewCustomerLocations = false;
        }
    }

    private void OnNewCustomerDistrictChanged(
        ChangeEventArgs eventArgs)
    {
        newCustomerModel.DistrictCode =
            eventArgs.Value?.ToString() ?? string.Empty;

        newCustomerLocationErrorMessage = null;
    }

    private async Task LookupNewCustomerTaxpayerAsync()
    {
        if (!CanLookupNewCustomerTaxpayer)
        {
            SetNewCustomerTaxpayerMessage(
                "Completa una identificación válida antes de consultar.",
                false);

            return;
        }

        isLookingUpNewCustomerTaxpayer = true;
        newCustomerTaxpayerMessage = null;
        wasNewCustomerTaxpayerLookupSuccessful = false;

        try
        {
            var result = await TaxpayerApiService.FindAsync(
                newCustomerModel.IdentificationNumber,
                cancellationTokenSource.Token);

            switch (result.Status)
            {
                case TaxpayerApiLookupStatus.Found:
                    ApplyNewCustomerTaxpayerLookup(
                        result.Taxpayer!);
                    break;

                case TaxpayerApiLookupStatus.NotFound:
                    SetNewCustomerTaxpayerMessage(
                        "La identificación no fue encontrada en Hacienda.",
                        false);
                    break;

                case TaxpayerApiLookupStatus.RateLimited:
                    SetNewCustomerTaxpayerMessage(
                        "Hacienda está limitando temporalmente las consultas. Inténtalo más tarde.",
                        false);
                    break;

                default:
                    SetNewCustomerTaxpayerMessage(
                        "No fue posible consultar Hacienda en este momento.",
                        false);
                    break;
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (HttpRequestException)
        {
            SetNewCustomerTaxpayerMessage(
                "No fue posible conectar con el servicio de consulta.",
                false);
        }
        finally
        {
            isLookingUpNewCustomerTaxpayer = false;
        }
    }

    private void ApplyNewCustomerTaxpayerLookup(
        TaxpayerLookupResponse taxpayer)
    {
        var identificationType =
            taxpayer.IdentificationTypeCode switch
            {
                "01" => IdentificationType.PhysicalPerson,
                "02" => IdentificationType.LegalEntity,
                "03" => IdentificationType.Dimex,
                _ => (IdentificationType?)null
            };

        if (!identificationType.HasValue)
        {
            SetNewCustomerTaxpayerMessage(
                "Hacienda devolvió un tipo de identificación que Revestik aún no admite.",
                false);

            return;
        }

        newCustomerModel.IdentificationType =
            identificationType.Value;

        newCustomerModel.Name = taxpayer.Name;

        SetNewCustomerTaxpayerMessage(
            "Identificación verificada. El nombre fue completado con los datos de Hacienda.",
            true);
    }

    private void SetNewCustomerTaxpayerMessage(
        string message,
        bool wasSuccessful)
    {
        newCustomerTaxpayerMessage = message;
        wasNewCustomerTaxpayerLookupSuccessful = wasSuccessful;
    }

    private bool IsValidNewCustomerIdentification(
        int requiredLength)
    {
        var identificationNumber =
            newCustomerModel.IdentificationNumber;

        return identificationNumber.Length == requiredLength &&
               identificationNumber.All(char.IsDigit) &&
               identificationNumber[0] != '0';
    }

    private async Task SaveNewCustomerAsync()
    {
        if (!HasCompleteNewCustomerLocation)
        {
            newCustomerErrorMessage =
                "Completa provincia, cantón, distrito y otras señas.";

            return;
        }

        isSavingNewCustomer = true;
        newCustomerErrorMessage = null;

        try
        {
            newCustomerModel.IsActive = true;

            var customer =
                await CustomerApiService.CreateAsync(
                    newCustomerModel,
                    cancellationTokenSource.Token);

            selectedCustomer =
                new CustomerListItemResponse(
                    customer.Id,
                    customer.IdentificationNumber,
                    customer.Name,
                    customer.Email,
                    customer.PhoneNumber,
                    customer.IdentificationType,
                    customer.IsActive);

            formModel.CustomerId = customer.Id;

            customerSearchResults = [];
            customerSearchTerm = string.Empty;
            hasSearchedCustomers = false;
            errorMessage = null;

            CloseNewCustomerForm();
        }
        catch (OperationCanceledException)
        {
        }
        catch (CustomerIdentificationConflictException)
        {
            newCustomerErrorMessage =
                "Ya existe un cliente con la misma identificación.";
        }
        catch (HttpRequestException)
        {
            newCustomerErrorMessage =
                "No fue posible guardar el cliente. Inténtalo nuevamente.";
        }
        finally
        {
            isSavingNewCustomer = false;
        }
    }

    private void StartNewQuotationLine()
    {
        CloseProductSearch();
        lineEditor = new QuotationLineFormModel();
        editingQuotationLine = null;
        isLineEditorOpen = true;
        errorMessage = null;
    }

    private void ConfirmQuotationLine()
    {
        if (!ValidateLineEditor())
        {
            return;
        }

        if (editingQuotationLine is null)
        {
            quotationLines.Add(lineEditor);
        }

        CloseProductSearch();
        lineEditor = new QuotationLineFormModel();
        editingQuotationLine = null;
        isLineEditorOpen = false;
        errorMessage = null;
    }

    private void EditQuotationLine(
        QuotationLineFormModel line)
    {
        if (isLineEditorOpen)
        {
            return;
        }

        CloseProductSearch();
        lineEditor = line;
        editingQuotationLine = line;
        isLineEditorOpen = true;
        errorMessage = null;
    }

    private void RemoveQuotationLine(
        QuotationLineFormModel line)
    {
        if (isLineEditorOpen)
        {
            return;
        }

        quotationLines.Remove(line);
        errorMessage = null;
    }

    private bool ValidateLineEditor()
    {
        errorMessage = null;
        quotationSuccessMessage = null;

        if (string.IsNullOrWhiteSpace(lineEditor.Description))
        {
            errorMessage = "Ingresa una descripción para la línea.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(lineEditor.Unit))
        {
            errorMessage = "Selecciona una unidad para la línea.";
            return false;
        }

        if (lineEditor.Quantity <= 0m)
        {
            errorMessage = "La cantidad debe ser mayor que cero.";
            return false;
        }

        if (lineEditor.UnitPrice <= 0m)
        {
            errorMessage = "El precio unitario debe ser mayor que cero.";
            return false;
        }

        if (lineEditor.TaxRate is not 0m and not 13m)
        {
            errorMessage = "Selecciona una tarifa de IVA válida.";
            return false;
        }

        return true;
    }

    private void AddQuotationCharge()
    {
        quotationCharges.Add(
            new QuotationChargeFormModel());
    }

    private void RemoveQuotationCharge(
        QuotationChargeFormModel charge)
    {
        quotationCharges.Remove(charge);
    }

    private void OpenProductSearch(
        QuotationLineFormModel line)
    {
        selectedProductLine = line;
        productSearchTerm = string.Empty;
        productSearchResults = [];
        hasSearchedProducts = false;
        errorMessage = null;
    }

    private async Task SearchProductsAsync()
    {
        errorMessage = null;
        hasSearchedProducts = true;

        if (selectedProductLine is null)
        {
            productSearchResults = [];
            return;
        }

        if (string.IsNullOrWhiteSpace(productSearchTerm))
        {
            productSearchResults = [];
            return;
        }

        isSearchingProducts = true;

        try
        {
            var result = await ProductApiService.GetPageAsync(
                new ProductListRequest
                {
                    Search = productSearchTerm.Trim(),
                    Page = 1,
                    PageSize = 20
                },
                cancellationTokenSource.Token);

            productSearchResults = result.Items;
        }
        catch (OperationCanceledException)
        {
        }
        catch (HttpRequestException)
        {
            errorMessage =
                "No fue posible buscar productos. Verifica la conexión con la API.";
        }
        finally
        {
            isSearchingProducts = false;
        }
    }

    private void SelectProduct(
        ProductListItemResponse product)
    {
        if (selectedProductLine is null)
        {
            return;
        }

        selectedProductLine.LinkProduct(product);

        CloseProductSearch();
        errorMessage = null;
    }

    private void UnlinkProduct(
        QuotationLineFormModel line)
    {
        line.UnlinkProduct();

        if (ReferenceEquals(selectedProductLine, line))
        {
            CloseProductSearch();
        }

        errorMessage = null;
    }

    private void CloseProductSearch()
    {
        selectedProductLine = null;
        productSearchTerm = string.Empty;
        productSearchResults = [];
        hasSearchedProducts = false;
        isSearchingProducts = false;
    }

    private async Task SaveDraftAsync()
    {
        if (isSavingQuotation)
        {
            return;
        }

        if (!ValidateQuotationBeforeSave())
        {
            return;
        }

        isSavingQuotation = true;
        errorMessage = null;
        quotationSuccessMessage = null;

        try
        {
            PrepareQuotationRequest();

            QuotationResponse? quotation;

            if (currentQuotationId.HasValue)
            {
                quotation = await QuotationApiService.UpdateAsync(
                    currentQuotationId.Value,
                    formModel,
                    cancellationTokenSource.Token);
            }
            else
            {
                quotation = await QuotationApiService.CreateAsync(
                    formModel,
                    cancellationTokenSource.Token);
            }

            if (quotation is null)
            {
                errorMessage =
                    "La cotización ya no existe o no pudo ser encontrada.";
                return;
            }

            ApplySavedQuotation(quotation);

            quotationSuccessMessage =
                $"Borrador {quotation.QuotationNumber} guardado correctamente.";
        }
        catch (OperationCanceledException)
        {
        }
        catch (HttpRequestException)
        {
            errorMessage =
                "No fue posible guardar la cotización. Revisa los datos e inténtalo nuevamente.";
        }
        finally
        {
            isSavingQuotation = false;
        }
    }

    private async Task IssueQuotationAsync()
    {
        if (isSavingQuotation)
        {
            return;
        }

        if (!ValidateQuotationBeforeSave())
        {
            return;
        }

        isSavingQuotation = true;
        errorMessage = null;
        quotationSuccessMessage = null;

        try
        {
            PrepareQuotationRequest();

            if (!currentQuotationId.HasValue)
            {
                var draft = await QuotationApiService.CreateAsync(
                    formModel,
                    cancellationTokenSource.Token);

                ApplySavedQuotation(draft);
            }

            var quotation = await QuotationApiService.IssueAsync(
                currentQuotationId!.Value,
                formModel,
                cancellationTokenSource.Token);

            if (quotation is null)
            {
                errorMessage =
                    "La cotización ya no existe o no pudo ser encontrada.";
                return;
            }

            ApplySavedQuotation(quotation);

            quotationSuccessMessage =
                $"Cotización {quotation.QuotationNumber} emitida correctamente.";
        }
        catch (OperationCanceledException)
        {
        }
        catch (HttpRequestException)
        {
            errorMessage =
                currentQuotationId.HasValue
                    ? "No fue posible emitir la cotización. El borrador se conserva y puedes intentarlo nuevamente."
                    : "No fue posible emitir la cotización. Revisa los datos e inténtalo nuevamente.";
        }
        finally
        {
            isSavingQuotation = false;
        }
    }

    private async Task DownloadQuotationPdfAsync()
    {
        if (!currentQuotationId.HasValue || isDownloadingQuotationPdf)
        {
            return;
        }

        isDownloadingQuotationPdf = true;
        errorMessage = null;

        try
        {
            var pdf = await QuotationApiService.GetPdfAsync(
                currentQuotationId.Value,
                cancellationTokenSource.Token);

            if (pdf is null)
            {
                errorMessage =
                    "La cotización ya no existe o no pudo ser encontrada.";
                return;
            }

            var fileName =
                $"{currentQuotationNumber ?? $"COT-{currentQuotationId.Value:000000}"}.pdf";

            await using var stream = new MemoryStream(pdf);
            using var streamReference = new DotNetStreamReference(stream);

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
                "No fue posible descargar el PDF de la cotización.";
        }
        catch (JSException)
        {
            errorMessage =
                "El PDF fue generado, pero el navegador no pudo iniciar la descarga.";
        }
        finally
        {
            isDownloadingQuotationPdf = false;
        }
    }

    private bool ValidateQuotationBeforeSave()
    {
        errorMessage = null;
        quotationSuccessMessage = null;

        if (selectedCustomer is null || formModel.CustomerId <= 0)
        {
            errorMessage =
                "Selecciona un cliente antes de guardar la cotización.";
            return false;
        }

        if (isLineEditorOpen)
        {
            errorMessage = quotationLines.Count == 0
                ? "Agrega la línea actual al resumen antes de guardar la cotización."
                : "Guarda los cambios de la línea actual antes de guardar la cotización.";
            return false;
        }

        if (quotationLines.Count == 0)
        {
            errorMessage =
                "Agrega al menos una línea a la cotización.";
            return false;
        }

        if (quotationCharges.Any(charge => charge.Amount <= 0m))
        {
            errorMessage =
                "Todos los cargos deben tener un monto mayor que cero.";
            return false;
        }

        if (quotationCharges.Any(charge =>
                charge.Type == QuotationChargeType.Other &&
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

    private void ApplySavedQuotation(QuotationResponse quotation)
    {
        currentQuotationId = quotation.Id;
        currentQuotationNumber = quotation.QuotationNumber;
        currentQuotationStatus = quotation.Status;
    }

    private void PrepareQuotationRequest()
    {
        formModel.Lines = quotationLines
            .Select(line => line.ToRequest())
            .ToList();

        formModel.Charges = quotationCharges
            .Select(charge => charge.ToRequest())
            .ToList();
    }

    private static CustomerUpsertRequest CreateNewCustomerModel()
    {
        return new CustomerUpsertRequest
        {
            IsActive = true
        };
    }

    public void Dispose()
    {
        cancellationTokenSource.Cancel();
        cancellationTokenSource.Dispose();
    }
}