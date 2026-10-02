using Microsoft.AspNetCore.Components;
using Revestik.Client.Services.Purchases;
using Revestik.Shared.Purchases;
using Revestik.Shared.Sales;

namespace Revestik.Client.Pages;

public partial class PurchasesHistory : IDisposable
{
    private readonly CancellationTokenSource cancellationTokenSource = new();

    private readonly PurchaseListRequest listRequest =
        new()
        {
            Page = 1,
            PageSize = 20
        };

    private readonly PurchaseAlertRequest alertRequest =
        new()
        {
            ShortWindowDays = 3,
            LongWindowDays = 7
        };

    private IReadOnlyList<PurchaseListItemResponse> purchases = [];
    private IReadOnlyList<PurchaseCurrencyApSummaryResponse>
        summaryCurrencies = [];
    private IReadOnlyList<PurchaseDueAlertItemResponse> alerts = [];

    private PurchaseResponse? selectedPurchase;
    private SupplierPurchaseHistoryResponse? supplierHistory;

    private bool overdueOnly;
    private int totalPages;

    private bool isLoadingPurchases;
    private bool isLoadingSummary;
    private bool isLoadingAlerts;
    private bool isLoadingDetail;

    private bool isPaymentFormOpen;
    private bool isRegisteringPayment;
    private PurchasePaymentRequest paymentRequest =
        CreatePaymentRequest();
    private DateOnly paymentDate =
        DateOnly.FromDateTime(DateTime.Today);

    private int? paymentPendingVoidId;
    private string voidReason = string.Empty;
    private bool isVoidingPayment;

    private string? errorMessage;
    private string? successMessage;

    [Inject]
    private IPurchaseApiService PurchaseApiService { get; set; } =
        default!;

    private static IReadOnlyList<PaymentMethod> PaymentMethods { get; } =
        Enum.GetValues<PaymentMethod>();

    private bool CanGoPrevious =>
        !isLoadingPurchases &&
        listRequest.Page > 1;

    private bool CanGoNext =>
        !isLoadingPurchases &&
        listRequest.Page < totalPages;

    protected override async Task OnInitializedAsync()
    {
        await Task.WhenAll(
            LoadPurchasesAsync(),
            LoadSummaryAsync(),
            LoadAlertsAsync());
    }

    private async Task LoadPurchasesAsync()
    {
        isLoadingPurchases = true;
        errorMessage = null;

        try
        {
            listRequest.OverdueOnly =
                overdueOnly ? true : null;

            var result =
                await PurchaseApiService.GetPageAsync(
                    listRequest,
                    cancellationTokenSource.Token);

            purchases = result.Items;
            totalPages = result.TotalPages;
        }
        catch (OperationCanceledException)
        {
        }
        catch (HttpRequestException)
        {
            errorMessage =
                "No fue posible cargar las compras.";
        }
        finally
        {
            isLoadingPurchases = false;
        }
    }

    private async Task LoadSummaryAsync()
    {
        isLoadingSummary = true;

        try
        {
            var summary =
                await PurchaseApiService.GetApSummaryAsync(
                    cancellationTokenSource.Token);

            summaryCurrencies = summary.Currencies;
        }
        catch (OperationCanceledException)
        {
        }
        catch (HttpRequestException)
        {
            errorMessage ??=
                "No fue posible cargar el resumen de cuentas por pagar.";
        }
        finally
        {
            isLoadingSummary = false;
        }
    }

    private async Task LoadAlertsAsync()
    {
        if (alertRequest.ShortWindowDays <= 0 ||
            alertRequest.LongWindowDays <
            alertRequest.ShortWindowDays)
        {
            errorMessage =
                "Las ventanas de alertas no son válidas.";
            return;
        }

        isLoadingAlerts = true;

        try
        {
            var result =
                await PurchaseApiService.GetDueAlertsAsync(
                    alertRequest,
                    cancellationTokenSource.Token);

            alerts = result.Items;
        }
        catch (OperationCanceledException)
        {
        }
        catch (HttpRequestException)
        {
            errorMessage ??=
                "No fue posible cargar las alertas.";
        }
        finally
        {
            isLoadingAlerts = false;
        }
    }

    private async Task SearchAsync()
    {
        listRequest.Page = 1;
        CloseDetail();
        await LoadPurchasesAsync();
    }

    private async Task ClearFiltersAsync()
    {
        listRequest.SupplierId = null;
        listRequest.DateFrom = null;
        listRequest.DateTo = null;
        listRequest.Currency = null;
        listRequest.PaymentType = null;
        listRequest.BalanceStatus = null;
        listRequest.OverdueOnly = null;
        listRequest.Page = 1;
        overdueOnly = false;

        CloseDetail();

        await LoadPurchasesAsync();
    }

    private async Task PreviousPageAsync()
    {
        if (!CanGoPrevious)
        {
            return;
        }

        listRequest.Page--;
        CloseDetail();
        await LoadPurchasesAsync();
    }

    private async Task NextPageAsync()
    {
        if (!CanGoNext)
        {
            return;
        }

        listRequest.Page++;
        CloseDetail();
        await LoadPurchasesAsync();
    }

    private async Task OpenPurchaseAsync(
        int id)
    {
        isLoadingDetail = true;
        errorMessage = null;
        successMessage = null;

        try
        {
            selectedPurchase =
                await PurchaseApiService.GetByIdAsync(
                    id,
                    cancellationTokenSource.Token);

            if (selectedPurchase is null)
            {
                errorMessage =
                    "La compra ya no existe.";
                return;
            }

            supplierHistory = null;
            ResetPaymentUi();
        }
        catch (OperationCanceledException)
        {
        }
        catch (HttpRequestException)
        {
            errorMessage =
                "No fue posible cargar el detalle de la compra.";
        }
        finally
        {
            isLoadingDetail = false;
        }
    }

    private void CloseDetail()
    {
        selectedPurchase = null;
        supplierHistory = null;
        ResetPaymentUi();
    }

    private async Task LoadSupplierHistoryAsync()
    {
        if (selectedPurchase is null)
        {
            return;
        }

        try
        {
            supplierHistory =
                await PurchaseApiService.GetSupplierHistoryAsync(
                    selectedPurchase.SupplierId,
                    new SupplierPurchaseHistoryRequest
                    {
                        Page = 1,
                        PageSize = 20
                    },
                    cancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (HttpRequestException)
        {
            errorMessage =
                "No fue posible cargar el historial del proveedor.";
        }
    }

    private void CloseSupplierHistory()
    {
        supplierHistory = null;
    }

    private void OpenPaymentForm()
    {
        if (selectedPurchase is null ||
            selectedPurchase.OutstandingAmount <= 0m)
        {
            return;
        }

        paymentRequest = CreatePaymentRequest();
        paymentRequest.Amount =
            selectedPurchase.OutstandingAmount;
        paymentDate =
            DateOnly.FromDateTime(DateTime.Today);
        isPaymentFormOpen = true;
        errorMessage = null;
        successMessage = null;
    }

    private void ClosePaymentForm()
    {
        if (!isRegisteringPayment)
        {
            isPaymentFormOpen = false;
        }
    }

    private async Task RegisterPaymentAsync()
    {
        if (selectedPurchase is null ||
            isRegisteringPayment)
        {
            return;
        }

        if (paymentRequest.Amount <= 0m ||
            paymentRequest.Amount >
            selectedPurchase.OutstandingAmount)
        {
            errorMessage =
                "El pago debe ser mayor que cero y no superar el saldo pendiente.";
            return;
        }

        if (selectedPurchase.Currency ==
                PurchaseCurrency.USD &&
            paymentRequest.ExchangeRate is null or <= 0m)
        {
            errorMessage =
                "Los pagos de una deuda en USD requieren el tipo de cambio del día.";
            return;
        }

        isRegisteringPayment = true;
        errorMessage = null;
        successMessage = null;

        try
        {
            paymentRequest.PaidAtUtc =
                ToUtc(paymentDate);

            var updated =
                await PurchaseApiService.RegisterPaymentAsync(
                    selectedPurchase.Id,
                    paymentRequest,
                    cancellationTokenSource.Token);

            if (updated is null)
            {
                errorMessage =
                    "La compra ya no existe.";
                return;
            }

            selectedPurchase = updated;
            isPaymentFormOpen = false;
            paymentRequest = CreatePaymentRequest();

            successMessage =
                updated.OutstandingAmount == 0m
                    ? "Pago registrado. La compra quedó pagada."
                    : "Pago parcial registrado correctamente.";

            await RefreshAfterFinancialChangeAsync();
        }
        catch (OperationCanceledException)
        {
        }
        catch (HttpRequestException)
        {
            errorMessage =
                "No fue posible registrar el pago.";
        }
        finally
        {
            isRegisteringPayment = false;
        }
    }

    private void RequestVoidPayment(
        int paymentId)
    {
        paymentPendingVoidId = paymentId;
        voidReason = string.Empty;
        errorMessage = null;
        successMessage = null;
    }

    private void CancelVoidPayment()
    {
        if (!isVoidingPayment)
        {
            paymentPendingVoidId = null;
            voidReason = string.Empty;
        }
    }

    private async Task VoidPaymentAsync()
    {
        if (selectedPurchase is null ||
            !paymentPendingVoidId.HasValue ||
            isVoidingPayment)
        {
            return;
        }

        var reason = voidReason.Trim();

        if (reason.Length is < 3 or > 1000)
        {
            errorMessage =
                "El motivo debe tener entre 3 y 1000 caracteres.";
            return;
        }

        isVoidingPayment = true;
        errorMessage = null;
        successMessage = null;

        try
        {
            var updated =
                await PurchaseApiService.VoidPaymentAsync(
                    selectedPurchase.Id,
                    paymentPendingVoidId.Value,
                    new VoidPurchasePaymentRequest
                    {
                        Reason = reason
                    },
                    cancellationTokenSource.Token);

            if (updated is null)
            {
                errorMessage =
                    "La compra o el pago ya no existe.";
                return;
            }

            selectedPurchase = updated;
            paymentPendingVoidId = null;
            voidReason = string.Empty;

            successMessage =
                "Pago anulado. El historial se conserva.";

            await RefreshAfterFinancialChangeAsync();
        }
        catch (OperationCanceledException)
        {
        }
        catch (HttpRequestException)
        {
            errorMessage =
                "No fue posible anular el pago.";
        }
        finally
        {
            isVoidingPayment = false;
        }
    }

    private async Task RefreshAfterFinancialChangeAsync()
    {
        await Task.WhenAll(
            LoadPurchasesAsync(),
            LoadSummaryAsync(),
            LoadAlertsAsync());

        if (supplierHistory is not null &&
            selectedPurchase is not null)
        {
            await LoadSupplierHistoryAsync();
        }
    }

    private void ResetPaymentUi()
    {
        isPaymentFormOpen = false;
        paymentRequest = CreatePaymentRequest();
        paymentDate =
            DateOnly.FromDateTime(DateTime.Today);
        paymentPendingVoidId = null;
        voidReason = string.Empty;
    }

    private static PurchasePaymentRequest
        CreatePaymentRequest() =>
        new()
        {
            PaymentMethod =
                PaymentMethod.BankTransfer,
            PaidAtUtc =
                DateTime.UtcNow
        };

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
            PurchasePaymentType.Cash => "Contado",
            PurchasePaymentType.Credit => "Crédito",
            _ => type.ToString()
        };

    private static string GetBalanceLabel(
        PurchaseListItemResponse purchase)
    {
        if (purchase.IsOverdue)
        {
            return "Vencida";
        }

        return purchase.BalanceStatus switch
        {
            PurchaseBalanceStatus.Pending =>
                "Pendiente",
            PurchaseBalanceStatus.PartiallyPaid =>
                "Abonada parcialmente",
            PurchaseBalanceStatus.Paid =>
                "Pagada",
            _ => purchase.BalanceStatus.ToString()
        };
    }

    private static string GetBalanceCssClass(
        PurchaseListItemResponse purchase)
    {
        if (purchase.IsOverdue)
        {
            return "status-badge danger-status";
        }

        return purchase.BalanceStatus switch
        {
            PurchaseBalanceStatus.Pending =>
                "status-badge warning-status",
            PurchaseBalanceStatus.PartiallyPaid =>
                "status-badge partial-status",
            PurchaseBalanceStatus.Paid =>
                "status-badge success-status",
            _ =>
                "status-badge neutral-status"
        };
    }

    private static string GetAlertCssClass(
        PurchaseDueAlertType type) =>
        type switch
        {
            PurchaseDueAlertType.Overdue =>
                "status-badge danger-status",
            PurchaseDueAlertType.DueToday =>
                "status-badge warning-status",
            PurchaseDueAlertType.DueWithinShortWindow =>
                "status-badge partial-status",
            _ =>
                "status-badge neutral-status"
        };

    private static string GetAlertLabel(
        PurchaseDueAlertItemResponse alert) =>
        alert.AlertType switch
        {
            PurchaseDueAlertType.Overdue =>
                $"Vencida hace {Math.Abs(alert.DaysUntilDue)} día(s)",
            PurchaseDueAlertType.DueToday =>
                "Vence hoy",
            PurchaseDueAlertType.DueWithinShortWindow =>
                $"Vence en {alert.DaysUntilDue} día(s)",
            PurchaseDueAlertType.DueWithinLongWindow =>
                $"Vence en {alert.DaysUntilDue} día(s)",
            _ =>
                alert.AlertType.ToString()
        };

    private static string GetPaymentMethodLabel(
        PaymentMethod method) =>
        method switch
        {
            PaymentMethod.Cash => "Efectivo",
            PaymentMethod.Sinpe => "SINPE",
            PaymentMethod.BankTransfer =>
                "Transferencia bancaria",
            PaymentMethod.InternationalTransfer =>
                "Transferencia internacional",
            PaymentMethod.Card => "Tarjeta",
            PaymentMethod.Other => "Otro",
            _ => method.ToString()
        };

    private static string GetPaymentStatusLabel(
        PurchasePaymentStatus status) =>
        status switch
        {
            PurchasePaymentStatus.Active => "Activo",
            PurchasePaymentStatus.Voided => "Anulado",
            _ => status.ToString()
        };

    private static string FormatDate(
        DateTime utcDate) =>
        utcDate.ToLocalTime()
            .ToString("dd/MM/yyyy");

    public void Dispose()
    {
        cancellationTokenSource.Cancel();
        cancellationTokenSource.Dispose();
    }
}