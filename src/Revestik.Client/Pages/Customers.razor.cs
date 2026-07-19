using Microsoft.AspNetCore.Components;
using Revestik.Client.Services.Customers;
using Revestik.Client.Services.Taxpayers;
using Revestik.Shared.Customers;
using Revestik.Shared.Taxpayers;

namespace Revestik.Client.Pages;

public partial class Customers : ComponentBase, IDisposable
{
    private readonly CancellationTokenSource cancellationTokenSource = new();

    private IReadOnlyList<CustomerResponse> customers = [];
    private CustomerUpsertRequest formModel = new();

    private string searchTerm = string.Empty;
    private string? errorMessage;
    private string? successMessage;
    private string? taxpayerLookupMessage;

    private int? editingCustomerId;
    private int? customerPendingDeactivationId;

    private bool isLoading;
    private bool isSaving;
    private bool isLookingUpTaxpayer;
    private bool wasTaxpayerLookupSuccessful;

    [Inject]
    private ICustomerApiService CustomerApiService { get; set; } = default!;

    [Inject]
    private ITaxpayerApiService TaxpayerApiService { get; set; } = default!;

    private string FormTitle =>
        editingCustomerId.HasValue
            ? "Editar cliente"
            : "Registrar cliente";

    private string SaveButtonText =>
        editingCustomerId.HasValue
            ? "Actualizar cliente"
            : "Guardar cliente";

    private string CustomerCountText =>
        customers.Count == 1
            ? "1 cliente"
            : $"{customers.Count} clientes";

    private string IdentificationRule =>
        formModel.IdentificationType switch
        {
            IdentificationType.PhysicalPerson =>
                "Debe contener 9 dígitos, sin guiones y sin cero al inicio.",

            IdentificationType.Dimex =>
                "Debe contener 11 o 12 dígitos, sin guiones y sin cero al inicio.",

            IdentificationType.LegalEntity =>
                "Debe contener 10 dígitos y sin guiones.",

            _ => string.Empty
        };

    private string IdentificationPlaceholder =>
        formModel.IdentificationType switch
        {
            IdentificationType.PhysicalPerson => "Ejemplo: 109870654",
            IdentificationType.Dimex => "Ejemplo: 12345678901",
            IdentificationType.LegalEntity => "Ejemplo: 3101234567",
            _ => "Seleccione primero el tipo de identificación"
        };

    private int IdentificationMaximumLength =>
        formModel.IdentificationType switch
        {
            IdentificationType.PhysicalPerson => 9,
            IdentificationType.Dimex => 12,
            IdentificationType.LegalEntity => 10,
            _ => 12
        };

    private bool CanLookupTaxpayer =>
        formModel.IdentificationType switch
        {
            IdentificationType.PhysicalPerson =>
                IsValidNumericIdentification(9),

            IdentificationType.Dimex =>
                IsValidNumericIdentification(11) ||
                IsValidNumericIdentification(12),

            IdentificationType.LegalEntity =>
                IsValidNumericIdentification(10),

            _ => false
        };

    private string TaxpayerLookupMessageCssClass =>
        wasTaxpayerLookupSuccessful
            ? "lookup-message lookup-success"
            : "lookup-message lookup-error";

    private string TaxpayerLookupMessageRole =>
        wasTaxpayerLookupSuccessful
            ? "status"
            : "alert";

    protected override async Task OnInitializedAsync()
    {
        await LoadCustomersAsync();
    }

    private async Task LoadCustomersAsync()
    {
        isLoading = true;
        errorMessage = null;

        try
        {
            customers = await CustomerApiService.GetAllAsync(
                searchTerm,
                cancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (HttpRequestException)
        {
            errorMessage =
                "No fue posible cargar los clientes. Verifica la conexión con la API.";
        }
        finally
        {
            isLoading = false;
        }
    }

    private async Task SaveCustomerAsync()
    {
        isSaving = true;
        errorMessage = null;
        successMessage = null;

        try
        {
            if (editingCustomerId.HasValue)
            {
                var updatedCustomer = await CustomerApiService.UpdateAsync(
                    editingCustomerId.Value,
                    formModel,
                    cancellationTokenSource.Token);

                if (updatedCustomer is null)
                {
                    errorMessage = "El cliente ya no existe.";
                    return;
                }

                successMessage = "Cliente actualizado correctamente.";
            }
            else
            {
                await CustomerApiService.CreateAsync(
                    formModel,
                    cancellationTokenSource.Token);

                successMessage = "Cliente registrado correctamente.";
            }

            ResetForm(clearMessages: false);
            await LoadCustomersAsync();
        }
        catch (OperationCanceledException)
        {
        }
        catch (HttpRequestException)
        {
            errorMessage =
                "No fue posible guardar el cliente. Inténtalo nuevamente.";
        }
        finally
        {
            isSaving = false;
        }
    }

    private async Task LookupTaxpayerAsync()
    {
        if (!CanLookupTaxpayer)
        {
            SetTaxpayerLookupMessage(
                "Completa una identificación válida antes de consultar.",
                wasSuccessful: false);

            return;
        }

        isLookingUpTaxpayer = true;
        taxpayerLookupMessage = null;
        wasTaxpayerLookupSuccessful = false;

        try
        {
            var result = await TaxpayerApiService.FindAsync(
                formModel.IdentificationNumber,
                cancellationTokenSource.Token);

            switch (result.Status)
            {
                case TaxpayerApiLookupStatus.Found:
                    ApplyTaxpayerLookup(result.Taxpayer!);
                    break;

                case TaxpayerApiLookupStatus.NotFound:
                    SetTaxpayerLookupMessage(
                        "La identificación no fue encontrada en Hacienda.",
                        wasSuccessful: false);
                    break;

                case TaxpayerApiLookupStatus.RateLimited:
                    SetTaxpayerLookupMessage(
                        "Hacienda está limitando temporalmente las consultas. Inténtalo más tarde.",
                        wasSuccessful: false);
                    break;

                default:
                    SetTaxpayerLookupMessage(
                        "No fue posible consultar Hacienda en este momento.",
                        wasSuccessful: false);
                    break;
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (HttpRequestException)
        {
            SetTaxpayerLookupMessage(
                "No fue posible conectar con el servicio de consulta.",
                wasSuccessful: false);
        }
        finally
        {
            isLookingUpTaxpayer = false;
        }
    }

    private void ApplyTaxpayerLookup(TaxpayerLookupResponse taxpayer)
    {
        var identificationType = taxpayer.IdentificationTypeCode switch
        {
            "01" => IdentificationType.PhysicalPerson,
            "02" => IdentificationType.LegalEntity,
            "03" => IdentificationType.Dimex,
            _ => (IdentificationType?)null
        };

        if (!identificationType.HasValue)
        {
            SetTaxpayerLookupMessage(
                "Hacienda devolvió un tipo de identificación que Revestik aún no admite.",
                wasSuccessful: false);

            return;
        }

        formModel.IdentificationType = identificationType.Value;
        formModel.Name = taxpayer.Name;

        SetTaxpayerLookupMessage(
            "Identificación verificada. El nombre fue completado con los datos de Hacienda.",
            wasSuccessful: true);
    }

    private void SetTaxpayerLookupMessage(
        string message,
        bool wasSuccessful)
    {
        taxpayerLookupMessage = message;
        wasTaxpayerLookupSuccessful = wasSuccessful;
    }

    private bool IsValidNumericIdentification(int requiredLength)
    {
        var identificationNumber = formModel.IdentificationNumber;

        return identificationNumber.Length == requiredLength &&
               identificationNumber.All(char.IsDigit) &&
               identificationNumber[0] != '0';
    }

    private async Task SearchAsync()
    {
        customerPendingDeactivationId = null;
        await LoadCustomersAsync();
    }

    private async Task ClearSearchAsync()
    {
        searchTerm = string.Empty;
        await LoadCustomersAsync();
    }

    private void EditCustomer(CustomerResponse customer)
    {
        editingCustomerId = customer.Id;
        customerPendingDeactivationId = null;
        errorMessage = null;
        successMessage = null;
        taxpayerLookupMessage = null;
        wasTaxpayerLookupSuccessful = false;

        formModel = new CustomerUpsertRequest
        {
            Name = customer.Name,
            IdentificationNumber = customer.IdentificationNumber ?? string.Empty,
            Email = customer.Email ?? string.Empty,
            PhoneNumber = customer.PhoneNumber ?? string.Empty,
            Address = customer.Address ?? string.Empty,
            IsActive = customer.IsActive
        };
    }

    private void RequestDeactivation(int customerId)
    {
        customerPendingDeactivationId = customerId;
        errorMessage = null;
        successMessage = null;
    }

    private void CancelDeactivation()
    {
        customerPendingDeactivationId = null;
    }

    private async Task DeactivateCustomerAsync(int customerId)
    {
        errorMessage = null;
        successMessage = null;

        try
        {
            var wasDeactivated = await CustomerApiService.DeactivateAsync(
                customerId,
                cancellationTokenSource.Token);

            if (!wasDeactivated)
            {
                errorMessage = "El cliente ya no existe.";
                return;
            }

            customerPendingDeactivationId = null;
            successMessage = "Cliente desactivado correctamente.";

            await LoadCustomersAsync();
        }
        catch (OperationCanceledException)
        {
        }
        catch (HttpRequestException)
        {
            errorMessage =
                "No fue posible desactivar el cliente. Inténtalo nuevamente.";
        }
    }

    private void ResetForm()
    {
        ResetForm(clearMessages: true);
    }

    private void ResetForm(bool clearMessages)
    {
        editingCustomerId = null;
        customerPendingDeactivationId = null;
        formModel = new CustomerUpsertRequest();
        taxpayerLookupMessage = null;
        wasTaxpayerLookupSuccessful = false;

        if (clearMessages)
        {
            errorMessage = null;
            successMessage = null;
        }
    }

    private static string GetStatusCssClass(CustomerResponse customer)
    {
        return customer.IsActive
            ? "status-badge active-status"
            : "status-badge inactive-status";
    }

    public void Dispose()
    {
        cancellationTokenSource.Cancel();
        cancellationTokenSource.Dispose();
    }
}