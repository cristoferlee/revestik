using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Revestik.Client.Services.Quotations;
using Revestik.Client.Services.Sales;
using Revestik.Shared.Quotations;

namespace Revestik.Client.Pages;

public partial class QuotesHistory :
    ComponentBase,
    IDisposable
{
    private readonly CancellationTokenSource
        cancellationTokenSource = new();

    private readonly QuotationListRequest
        listRequest = new()
        {
            Page = 1,
            PageSize = 20
        };

    private IReadOnlyList<QuotationListItemResponse>
        quotations = [];

    private QuotationResponse? selectedQuotation;
    private QuotationListItemResponse?
        selectedListItem;

    private DateTime? dateFrom;
    private DateTime? dateTo;

    private int totalPages;

    private string? errorMessage;
    private string? successMessage;

    private bool isLoadingList;
    private bool isLoadingDetail;
    private bool isDownloadingPdf;
    private bool isConverting;

    [Inject]
    private IQuotationApiService QuotationApiService
        { get; set; } = default!;

    [Inject]
    private ISaleApiService SaleApiService
        { get; set; } = default!;

    [Inject]
    private IJSRuntime JSRuntime
        { get; set; } = default!;

    [Inject]
    private NavigationManager Navigator
        { get; set; } = default!;

    private bool CanGoPrevious =>
        !isLoadingList &&
        listRequest.Page > 1;

    private bool CanGoNext =>
        !isLoadingList &&
        listRequest.Page < totalPages;

    protected override async Task OnInitializedAsync()
    {
        await LoadQuotationsAsync();
    }

    private async Task LoadQuotationsAsync()
    {
        isLoadingList = true;
        errorMessage = null;

        try
        {
            ApplyDateFilters();

            var result =
                await QuotationApiService.GetPageAsync(
                    listRequest,
                    cancellationTokenSource.Token);

            quotations =
                result.Items;

            totalPages =
                result.TotalPages;
        }
        catch (OperationCanceledException)
        {
        }
        catch (HttpRequestException)
        {
            errorMessage =
                "No fue posible cargar las cotizaciones. Verifica la conexión con la API.";
        }
        finally
        {
            isLoadingList = false;
        }
    }

    private async Task SearchAsync()
    {
        listRequest.Page = 1;
        CloseDetail();

        await LoadQuotationsAsync();
    }

    private async Task ClearFiltersAsync()
    {
        listRequest.Search = null;
        listRequest.Status = null;
        listRequest.Currency = null;
        listRequest.Page = 1;

        dateFrom = null;
        dateTo = null;

        CloseDetail();

        await LoadQuotationsAsync();
    }

    private async Task GoPreviousAsync()
    {
        if (!CanGoPrevious)
        {
            return;
        }

        listRequest.Page--;
        CloseDetail();

        await LoadQuotationsAsync();
    }

    private async Task GoNextAsync()
    {
        if (!CanGoNext)
        {
            return;
        }

        listRequest.Page++;
        CloseDetail();

        await LoadQuotationsAsync();
    }

    private async Task OpenQuotationAsync(
        int quotationId)
    {
        isLoadingDetail = true;
        errorMessage = null;
        successMessage = null;

        try
        {
            var quotation =
                await QuotationApiService.GetByIdAsync(
                    quotationId,
                    cancellationTokenSource.Token);

            if (quotation is null)
            {
                errorMessage =
                    "La cotización ya no existe o no pudo ser encontrada.";
                return;
            }

            selectedQuotation =
                quotation;

            selectedListItem =
                quotations.FirstOrDefault(
                    item =>
                        item.Id == quotationId);
        }
        catch (OperationCanceledException)
        {
        }
        catch (HttpRequestException)
        {
            errorMessage =
                "No fue posible cargar el detalle de la cotización.";
        }
        finally
        {
            isLoadingDetail = false;
        }
    }

    private void CloseDetail()
    {
        selectedQuotation = null;
        selectedListItem = null;
    }

    private async Task ConvertToSaleAsync()
    {
        if (selectedQuotation is null ||
            selectedQuotation.Status !=
                QuotationStatus.Issued ||
            selectedListItem?.ConvertedSaleId
                is not null ||
            isConverting)
        {
            return;
        }

        isConverting = true;
        errorMessage = null;
        successMessage = null;

        try
        {
            var sale =
                await SaleApiService
                    .CreateFromQuotationAsync(
                        selectedQuotation.Id,
                        cancellationTokenSource.Token);

            if (sale is null)
            {
                errorMessage =
                    "La cotización ya no existe o no pudo convertirse.";
                return;
            }

            Navigator.NavigateTo(
                $"/sales?draftId={sale.Id}");
        }
        catch (OperationCanceledException)
        {
        }
        catch (HttpRequestException)
        {
            errorMessage =
                "No fue posible convertir la cotización en venta.";
        }
        finally
        {
            isConverting = false;
        }
    }

    private async Task DownloadPdfAsync()
    {
        if (selectedQuotation is null ||
            isDownloadingPdf)
        {
            return;
        }

        isDownloadingPdf = true;
        errorMessage = null;

        try
        {
            var pdf =
                await QuotationApiService.GetPdfAsync(
                    selectedQuotation.Id,
                    cancellationTokenSource.Token);

            if (pdf is null)
            {
                errorMessage =
                    "La cotización ya no existe o no pudo ser encontrada.";
                return;
            }

            var fileName =
                $"{selectedQuotation.QuotationNumber}.pdf";

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
                "No fue posible descargar el PDF de la cotización.";
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

    private string GetSelectedStatusLabel()
    {
        if (selectedListItem?.ConvertedSaleId
            is not null)
        {
            return "Convertida en venta";
        }

        return selectedQuotation?.Status switch
        {
            QuotationStatus.Draft =>
                "Borrador",

            QuotationStatus.Issued =>
                "Emitida",

            _ =>
                "—"
        };
    }

    private static string GetStatusLabel(
        QuotationListItemResponse quotation)
    {
        if (quotation.ConvertedSaleId.HasValue)
        {
            return "Convertida";
        }

        return quotation.Status switch
        {
            QuotationStatus.Draft =>
                "Borrador",

            QuotationStatus.Issued =>
                "Emitida",

            _ =>
                quotation.Status.ToString()
        };
    }

    private static string GetStatusCssClass(
        QuotationListItemResponse quotation)
    {
        if (quotation.ConvertedSaleId.HasValue)
        {
            return "status-badge converted-status";
        }

        return quotation.Status switch
        {
            QuotationStatus.Draft =>
                "status-badge neutral-status",

            QuotationStatus.Issued =>
                "status-badge success-status",

            _ =>
                "status-badge neutral-status"
        };
    }

    private string GetConvertedSaleHref(
        int saleId)
    {
        if (selectedListItem is null ||
            string.IsNullOrWhiteSpace(
                selectedListItem.ConvertedSaleNumber))
        {
            return $"/sales?draftId={saleId}";
        }

        return "/sales/history";
    }

    private static string FormatDate(
        DateTime utcDate) =>
        utcDate.ToLocalTime()
            .ToString("dd/MM/yyyy");

    private static string FormatDateTime(
        DateTime utcDate) =>
        utcDate.ToLocalTime()
            .ToString("dd/MM/yyyy HH:mm");

    public void Dispose()
    {
        cancellationTokenSource.Cancel();
        cancellationTokenSource.Dispose();
    }
}