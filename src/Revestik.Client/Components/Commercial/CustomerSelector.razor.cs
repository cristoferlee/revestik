using Microsoft.AspNetCore.Components;
using Revestik.Client.Services.Customers;
using Revestik.Client.Services.Locations;
using Revestik.Client.Services.Taxpayers;
using Revestik.Shared.Customers;
using Revestik.Shared.Locations;
using Revestik.Shared.Taxpayers;

namespace Revestik.Client.Components.Commercial;

public partial class CustomerSelector : ComponentBase, IDisposable
{
    private readonly CancellationTokenSource cancellationTokenSource = new();

    private IReadOnlyList<CustomerListItemResponse> customerSearchResults = [];
    private IReadOnlyList<LocationOptionResponse> newCustomerProvinces = [];
    private IReadOnlyList<LocationOptionResponse> newCustomerCantons = [];
    private IReadOnlyList<LocationOptionResponse> newCustomerDistricts = [];

    private CustomerUpsertRequest newCustomerModel = CreateNewCustomerModel();

    private string customerSearchTerm = string.Empty;
    private string? newCustomerErrorMessage;
    private string? newCustomerTaxpayerMessage;
    private string? newCustomerLocationErrorMessage;

    private bool isSearchingCustomers;
    private bool hasSearchedCustomers;
    private bool isNewCustomerFormOpen;
    private bool isSavingNewCustomer;
    private bool isLookingUpNewCustomerTaxpayer;
    private bool isLoadingNewCustomerLocations;
    private bool wasNewCustomerTaxpayerLookupSuccessful;

    private int customerSearchPage = 1;
    private int customerSearchTotalPages;

    [Parameter]
    public CustomerListItemResponse? SelectedCustomer { get; set; }

    [Parameter]
    public bool Disabled { get; set; }

    [Parameter]
    public EventCallback<CustomerListItemResponse?> SelectedCustomerChanged
        { get; set; }

    [Inject]
    private ICustomerApiService CustomerApiService { get; set; } = default!;

    [Inject]
    private ITaxpayerApiService TaxpayerApiService { get; set; } = default!;

    [Inject]
    private ILocationApiService LocationApiService { get; set; } = default!;

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
        customerSearchPage = 1;
        await LoadCustomerSearchResultsAsync();
    }

    private async Task LoadCustomerSearchResultsAsync()
    {
        hasSearchedCustomers = true;
        isSearchingCustomers = true;

        try
        {
            var result = await CustomerApiService.GetPageAsync(
                new CustomerListRequest
                {
                    Search = string.IsNullOrWhiteSpace(customerSearchTerm)
                        ? null
                        : customerSearchTerm.Trim(),
                    Page = customerSearchPage,
                    PageSize = 20
                },
                cancellationTokenSource.Token);

            customerSearchResults = result.Items
                .Where(customer => customer.IsActive)
                .ToList();

            customerSearchTotalPages = result.TotalPages;
        }
        catch (OperationCanceledException)
        {
        }
        catch (HttpRequestException)
        {
            customerSearchResults = [];
            customerSearchTotalPages = 0;
        }
        finally
        {
            isSearchingCustomers = false;
        }
    }

    private async Task GoToPreviousCustomerPageAsync()
    {
        if (isSearchingCustomers || customerSearchPage <= 1)
        {
            return;
        }

        customerSearchPage--;
        await LoadCustomerSearchResultsAsync();
    }

    private async Task GoToNextCustomerPageAsync()
    {
        if (isSearchingCustomers ||
            customerSearchPage >= customerSearchTotalPages)
        {
            return;
        }

        customerSearchPage++;
        await LoadCustomerSearchResultsAsync();
    }

    private async Task SelectCustomerAsync(
        CustomerListItemResponse customer)
    {
        ClearCustomerSearch();
        CloseNewCustomerForm();

        await SelectedCustomerChanged.InvokeAsync(customer);
    }

    private async Task ClearSelectedCustomerAsync()
    {
        ClearCustomerSearch();
        CloseNewCustomerForm();

        await SelectedCustomerChanged.InvokeAsync(null);
    }

    private void ClearCustomerSearch()
    {
        customerSearchTerm = string.Empty;
        customerSearchResults = [];
        customerSearchPage = 1;
        customerSearchTotalPages = 0;
        hasSearchedCustomers = false;
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
                    ApplyNewCustomerTaxpayerLookup(result.Taxpayer!);
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

            var selectedCustomer =
                new CustomerListItemResponse(
                    customer.Id,
                    customer.IdentificationNumber,
                    customer.Name,
                    customer.Email,
                    customer.PhoneNumber,
                    customer.IdentificationType,
                    customer.IsActive);

            ClearCustomerSearch();
            CloseNewCustomerForm();

            await SelectedCustomerChanged.InvokeAsync(selectedCustomer);
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