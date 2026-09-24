using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Revestik.Client.Services.Sales;
using Revestik.Shared.Sales;

namespace Revestik.Client.Pages;

public partial class SalesHistory : ComponentBase, IDisposable
{
    private readonly CancellationTokenSource cancellationTokenSource = new();

    private readonly SaleListRequest listRequest = new()
    {
        Page = 1,
        PageSize = 20
    };

    private IReadOnlyList<SaleListItemResponse> sales = [];
    private IReadOnlyList<SaleCurrencySummaryResponse>
        summaryCurrencies = [];

    private SaleResponse? selectedSale;

    private DateTime? dateFrom;
    private DateTime? dateTo;

    private int totalPages;

    private string? errorMessage;
    private string? successMessage;

    private bool isLoadingSales;
    private bool isLoadingSummary;
    private bool isLoadingDetail;
    private bool isDownloadingPdf;

    private bool isPaymentFormOpen;
    private bool isFullPayment;
    private bool isRegisteringPayment;

    private SalePaymentRequest paymentRequest =
        CreatePaymentRequest();

    private DateTime paymentDate = DateTime.Today;

    private int? paymentPendingVoidId;
    private string voidPaymentReason = string.Empty;
    private bool isVoidingPayment;

    private SaleLifecycleAction? lifecycleAction;
    private string lifecycleReason = string.Empty;
    private bool isProcessingLifecycle;

    [Inject]
    private ISaleApiService SaleApiService { get; set; } =
        default!;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } =
        default!;

    [Inject]
    private NavigationManager Navigator { get; set; } =
        default!;

    private bool HasActivePayments =>
        selectedSale?.Payments.Any(
            payment => payment.Status == SalePaymentStatus.Active) is true;

    private bool CanGoPrevious =>
        !isLoadingSales &&
        listRequest.Page > 1;

    private bool CanGoNext =>
        !isLoadingSales &&
        listRequest.Page < totalPages;

    protected override async Task OnInitializedAsync()
    {
        await Task.WhenAll(
            LoadSalesAsync(),
            LoadSummaryAsync());
    }

    private async Task LoadSalesAsync()
    {
        isLoadingSales = true;
        errorMessage = null;

        try
        {
            ApplyDateFilters();

            var result =
                await SaleApiService.GetPageAsync(
                    listRequest,
                    cancellationTokenSource.Token);

            sales = result.Items;
            totalPages = result.TotalPages;
        }
        catch (OperationCanceledException)
        {
        }
        catch (HttpRequestException)
        {
            errorMessage =
                "No fue posible cargar las ventas. Verifica la conexión con la API.";
        }
        finally
        {
            isLoadingSales = false;
        }
    }

    private async Task LoadSummaryAsync()
    {
        isLoadingSummary = true;

        try
        {
            var summary =
                await SaleApiService.GetSummaryAsync(
                    cancellationTokenSource.Token);

            summaryCurrencies = summary.Currencies
                .OrderBy(item => item.Currency)
                .ToList();
        }
        catch (OperationCanceledException)
        {
        }
        catch (HttpRequestException)
        {
            errorMessage ??=
                "No fue posible cargar el resumen de ventas.";
        }
        finally
        {
            isLoadingSummary = false;
        }
    }

    private async Task SearchAsync()
    {
        listRequest.Page = 1;
        CloseDetail();

        await LoadSalesAsync();
    }

    private async Task ClearFiltersAsync()
    {
        listRequest.Search = null;
        listRequest.Status = null;
        listRequest.BalanceStatus = null;
        listRequest.Currency = null;
        listRequest.Page = 1;

        dateFrom = null;
        dateTo = null;

        CloseDetail();

        await LoadSalesAsync();
    }

    private async Task GoPreviousAsync()
    {
        if (!CanGoPrevious)
        {
            return;
        }

        listRequest.Page--;
        CloseDetail();

        await LoadSalesAsync();
    }

    private async Task GoNextAsync()
    {
        if (!CanGoNext)
        {
            return;
        }

        listRequest.Page++;
        CloseDetail();

        await LoadSalesAsync();
    }

    private async Task OpenSaleAsync(
        int saleId)
    {
        isLoadingDetail = true;
        errorMessage = null;
        successMessage = null;

        try
        {
            var sale =
                await SaleApiService.GetByIdAsync(
                    saleId,
                    cancellationTokenSource.Token);

            if (sale is null)
            {
                errorMessage =
                    "La venta ya no existe o no pudo ser encontrada.";
                return;
            }

            selectedSale = sale;
            ResetPaymentUi();
            ResetLifecycleUi();
        }
        catch (OperationCanceledException)
        {
        }
        catch (HttpRequestException)
        {
            errorMessage =
                "No fue posible cargar el detalle de la venta.";
        }
        finally
        {
            isLoadingDetail = false;
        }
    }

    private void CloseDetail()
    {
        selectedSale = null;
        ResetPaymentUi();
        ResetLifecycleUi();
    }

    private void OpenVoidSalePanel()
    {
        if (selectedSale is null ||
            selectedSale.Status != SaleStatus.Issued ||
            HasActivePayments)
        {
            return;
        }

        lifecycleAction = SaleLifecycleAction.Void;
        lifecycleReason = string.Empty;
        errorMessage = null;
        successMessage = null;
    }

    private void OpenReplacementPanel()
    {
        if (selectedSale is null ||
            selectedSale.Status != SaleStatus.Issued)
        {
            return;
        }

        lifecycleAction = SaleLifecycleAction.Replacement;
        lifecycleReason = string.Empty;
        errorMessage = null;
        successMessage = null;
    }

    private void CloseLifecyclePanel()
    {
        if (isProcessingLifecycle)
        {
            return;
        }

        ResetLifecycleUi();
    }

    private async Task ConfirmLifecycleAsync()
    {
        if (selectedSale is null ||
            lifecycleAction is null ||
            isProcessingLifecycle)
        {
            return;
        }

        var reason = lifecycleReason.Trim();

        if (reason.Length is < 3 or > 1000)
        {
            errorMessage =
                "El motivo debe tener entre 3 y 1000 caracteres.";
            return;
        }

        if (lifecycleAction == SaleLifecycleAction.Void &&
            HasActivePayments)
        {
            errorMessage =
                "La venta tiene pagos activos. Corrige o anula esos pagos antes de anular la venta.";
            return;
        }

        isProcessingLifecycle = true;
        errorMessage = null;
        successMessage = null;

        try
        {
            var request = new VoidSaleRequest
            {
                Reason = reason
            };

            if (lifecycleAction == SaleLifecycleAction.Void)
            {
                var voidedSale =
                    await SaleApiService.VoidAsync(
                        selectedSale.Id,
                        request,
                        cancellationTokenSource.Token);

                if (voidedSale is null)
                {
                    errorMessage =
                        "La venta ya no existe o no pudo ser encontrada.";
                    return;
                }

                selectedSale = voidedSale;
                ResetLifecycleUi();

                successMessage =
                    $"Venta {voidedSale.SaleNumber} anulada correctamente.";

                await Task.WhenAll(
                    LoadSalesAsync(),
                    LoadSummaryAsync());

                return;
            }

            var replacement =
                await SaleApiService.CreateReplacementAsync(
                    selectedSale.Id,
                    request,
                    cancellationTokenSource.Token);

            if (replacement is null)
            {
                errorMessage =
                    "La venta ya no existe o no pudo ser encontrada.";
                return;
            }

            await Task.WhenAll(
                LoadSalesAsync(),
                LoadSummaryAsync());

            Navigator.NavigateTo(
                $"/sales?draftId={replacement.Id}");
        }
        catch (OperationCanceledException)
        {
        }
        catch (HttpRequestException)
        {
            errorMessage =
                lifecycleAction == SaleLifecycleAction.Void
                    ? "No fue posible anular la venta. Revisa su estado financiero e inténtalo nuevamente."
                    : "No fue posible crear el reemplazo. Revisa el estado de la venta e inténtalo nuevamente.";
        }
        finally
        {
            isProcessingLifecycle = false;
        }
    }

    private void ResetLifecycleUi()
    {
        lifecycleAction = null;
        lifecycleReason = string.Empty;
    }

    private void ApplyDateFilters()
    {
        listRequest.DateFromUtc =
            dateFrom.HasValue
                ? DateTime.SpecifyKind(
                        dateFrom.Value.Date,
                        DateTimeKind.Local)
                    .ToUniversalTime()
                : null;

        listRequest.DateToUtc =
            dateTo.HasValue
                ? DateTime.SpecifyKind(
                        dateTo.Value.Date
                            .AddDays(1)
                            .AddTicks(-1),
                        DateTimeKind.Local)
                    .ToUniversalTime()
                : null;
    }

    private void OpenPartialPayment()
    {
        if (!CanRegisterPayment())
        {
            return;
        }

        paymentRequest = CreatePaymentRequest();
        paymentDate = DateTime.Today;
        isFullPayment = false;
        isPaymentFormOpen = true;
        errorMessage = null;
        successMessage = null;
    }

    private void OpenFullPayment()
    {
        if (!CanRegisterPayment())
        {
            return;
        }

        paymentRequest = CreatePaymentRequest();
        paymentRequest.Amount =
            selectedSale!.OutstandingAmount;

        paymentDate = DateTime.Today;
        isFullPayment = true;
        isPaymentFormOpen = true;
        errorMessage = null;
        successMessage = null;
    }

    private bool CanRegisterPayment() =>
        selectedSale is not null &&
        selectedSale.Status == SaleStatus.Issued &&
        selectedSale.OutstandingAmount > 0m;

    private void ClosePaymentForm()
    {
        if (isRegisteringPayment)
        {
            return;
        }

        isPaymentFormOpen = false;
        isFullPayment = false;
        paymentRequest = CreatePaymentRequest();
        paymentDate = DateTime.Today;
    }

    private async Task RegisterPaymentAsync()
    {
        if (selectedSale is null ||
            isRegisteringPayment)
        {
            return;
        }

        if (paymentRequest.Amount <= 0m)
        {
            errorMessage =
                "El monto del pago debe ser mayor que cero.";
            return;
        }

        if (paymentRequest.Amount >
            selectedSale.OutstandingAmount)
        {
            errorMessage =
                "El pago no puede superar el saldo pendiente.";
            return;
        }

        isRegisteringPayment = true;
        errorMessage = null;
        successMessage = null;

        try
        {
            paymentRequest.PaidAtUtc =
                DateTime.SpecifyKind(
                        paymentDate.Date,
                        DateTimeKind.Local)
                    .ToUniversalTime();

            var updatedSale =
                await SaleApiService.RegisterPaymentAsync(
                    selectedSale.Id,
                    paymentRequest,
                    cancellationTokenSource.Token);

            if (updatedSale is null)
            {
                errorMessage =
                    "La venta ya no existe o no pudo ser encontrada.";
                return;
            }

            selectedSale = updatedSale;

            isPaymentFormOpen = false;
            isFullPayment = false;
            paymentRequest = CreatePaymentRequest();

            successMessage =
                updatedSale.OutstandingAmount == 0m
                    ? "Pago registrado. La venta quedó cancelada."
                    : "Pago parcial registrado correctamente.";

            await Task.WhenAll(
                LoadSalesAsync(),
                LoadSummaryAsync());
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
            isRegisteringPayment = false;
        }
    }

    private void RequestVoidPayment(
        int paymentId)
    {
        paymentPendingVoidId = paymentId;
        voidPaymentReason = string.Empty;
        errorMessage = null;
        successMessage = null;
    }

    private void CancelVoidPayment()
    {
        if (isVoidingPayment)
        {
            return;
        }

        paymentPendingVoidId = null;
        voidPaymentReason = string.Empty;
    }

    private async Task VoidPaymentAsync()
    {
        if (selectedSale is null ||
            !paymentPendingVoidId.HasValue ||
            isVoidingPayment)
        {
            return;
        }

        var reason = voidPaymentReason.Trim();

        if (reason.Length is < 3 or > 1000)
        {
            errorMessage =
                "El motivo de anulación debe tener entre 3 y 1000 caracteres.";
            return;
        }

        isVoidingPayment = true;
        errorMessage = null;
        successMessage = null;

        try
        {
            var updatedSale =
                await SaleApiService.VoidPaymentAsync(
                    selectedSale.Id,
                    paymentPendingVoidId.Value,
                    new VoidSalePaymentRequest
                    {
                        Reason = reason
                    },
                    cancellationTokenSource.Token);

            if (updatedSale is null)
            {
                errorMessage =
                    "La venta o el pago ya no existe.";
                return;
            }

            selectedSale = updatedSale;
            paymentPendingVoidId = null;
            voidPaymentReason = string.Empty;

            successMessage =
                "Pago anulado correctamente. El historial se conserva.";

            await Task.WhenAll(
                LoadSalesAsync(),
                LoadSummaryAsync());
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

    private async Task DownloadPdfAsync()
    {
        if (selectedSale is null ||
            selectedSale.Status == SaleStatus.Draft ||
            isDownloadingPdf)
        {
            return;
        }

        isDownloadingPdf = true;
        errorMessage = null;

        try
        {
            var pdf =
                await SaleApiService.GetPdfAsync(
                    selectedSale.Id,
                    cancellationTokenSource.Token);

            if (pdf is null)
            {
                errorMessage =
                    "La venta ya no existe o no pudo ser encontrada.";
                return;
            }

            var fileName =
                $"{selectedSale.SaleNumber}.pdf";

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
            isDownloadingPdf = false;
        }
    }

    private void ResetPaymentUi()
    {
        isPaymentFormOpen = false;
        isFullPayment = false;

        paymentRequest = CreatePaymentRequest();
        paymentDate = DateTime.Today;

        paymentPendingVoidId = null;
        voidPaymentReason = string.Empty;
    }

    private static SalePaymentRequest
        CreatePaymentRequest() =>
        new()
        {
            PaymentMethod = PaymentMethod.Cash,
            PaidAtUtc = DateTime.UtcNow
        };

    private static string GetSaleStatusLabel(
        SaleStatus status) =>
        status switch
        {
            SaleStatus.Draft => "Borrador",
            SaleStatus.Issued => "Emitida",
            SaleStatus.Voided => "Anulada",
            _ => status.ToString()
        };

    private static string GetBalanceStatusLabel(
        SaleBalanceStatus status) =>
        status switch
        {
            SaleBalanceStatus.Pending =>
                "Pendiente",

            SaleBalanceStatus.PartiallyPaid =>
                "Abonada parcialmente",

            SaleBalanceStatus.Paid =>
                "Cancelada",

            _ => status.ToString()
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

    private static string GetChargeTypeLabel(
        SaleChargeType type) =>
        type switch
        {
            SaleChargeType.Service => "Servicio",
            SaleChargeType.Transport => "Transporte",
            SaleChargeType.Installation => "Instalación",
            SaleChargeType.Other => "Otro",
            _ => type.ToString()
        };

    private static string GetSaleStatusCssClass(
        SaleStatus status) =>
        status switch
        {
            SaleStatus.Draft =>
                "status-badge neutral-status",

            SaleStatus.Issued =>
                "status-badge success-status",

            SaleStatus.Voided =>
                "status-badge danger-status",

            _ =>
                "status-badge neutral-status"
        };

    private static string GetBalanceStatusCssClass(
        SaleBalanceStatus status) =>
        status switch
        {
            SaleBalanceStatus.Pending =>
                "status-badge warning-status",

            SaleBalanceStatus.PartiallyPaid =>
                "status-badge partial-status",

            SaleBalanceStatus.Paid =>
                "status-badge success-status",

            _ =>
                "status-badge neutral-status"
        };

    private static string FormatDate(
        DateTime utcDate) =>
        utcDate.ToLocalTime()
            .ToString("dd/MM/yyyy");

    private static string FormatDateTime(
        DateTime utcDate) =>
        utcDate.ToLocalTime()
            .ToString("dd/MM/yyyy HH:mm");

    private enum SaleLifecycleAction
    {
        Void,
        Replacement
    }

    public void Dispose()
    {
        cancellationTokenSource.Cancel();
        cancellationTokenSource.Dispose();
    }
}