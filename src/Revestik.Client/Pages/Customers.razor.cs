using Microsoft.AspNetCore.Components;
using Revestik.Client.Services.Customers;
using Revestik.Shared.Customers;

namespace Revestik.Client.Pages;

public partial class Customers : ComponentBase, IDisposable
{
    private readonly CancellationTokenSource cancellationTokenSource = new();

    private IReadOnlyList<CustomerResponse> customers = [];
    private CustomerUpsertRequest formModel = new();

    private string searchTerm = string.Empty;
    private string? errorMessage;
    private string? successMessage;

    private int? editingCustomerId;
    private int? customerPendingDeactivationId;

    private bool isLoading;
    private bool isSaving;

    [Inject]
    private ICustomerApiService CustomerApiService { get; set; } = default!;

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
            IdentificationType.PhysicalPerson =>
                "Ejemplo: 123456789",

            IdentificationType.Dimex =>
                "Ejemplo: 12345678901 o 123456789012",

            IdentificationType.LegalEntity =>
                "Ejemplo: 3101000000",

            _ => "Seleccione primer el tipo de identificación"
        };
    private int IdentificationMaximumLength =>
        formModel.IdentificationType switch
        {
            IdentificationType.PhysicalPerson => 9,
            IdentificationType.Dimex => 12,
            IdentificationType.LegalEntity => 10,
            _ => 12
        };

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

        formModel = new CustomerUpsertRequest
        {
            Name = customer.Name,
            IdentificationNumber = customer.IdentificationNumber,
            Email = customer.Email,
            PhoneNumber = customer.PhoneNumber,
            Address = customer.Address,
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