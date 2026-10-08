using Microsoft.AspNetCore.Components;
using Revestik.Client.Services.Expenses;
using Revestik.Shared.Expenses;
using Revestik.Shared.Integrations.Gmail;

namespace Revestik.Client.Pages;

public partial class Expenses : ComponentBase, IDisposable
{
    private readonly CancellationTokenSource cancellationTokenSource = new();

    private readonly ExpenseListRequest listRequest = new()
    {
        Page = 1,
        PageSize = 20
    };

    private readonly BankVoucherListRequest reviewVoucherRequest = new()
    {
        Status = BankVoucherStatus.NeedsReview,
        Page = 1,
        PageSize = 10
    };

    private readonly BankVoucherListRequest historyVoucherRequest = new()
    {
        ExcludeNeedsReview = true,
        Page = 1,
        PageSize = 10
    };

    private IReadOnlyList<ExpenseListItemResponse> expenses = [];
    private IReadOnlyList<BankVoucherReviewItemResponse> reviewVouchers = [];
    private IReadOnlyList<BankVoucherReviewItemResponse> historyVouchers = [];
    private ExpenseCreateRequest formModel = CreateDefaultForm();

    private ExpenseSummaryResponse summary = new(0, 0m);
    private ExpenseConsolidatedSummaryResponse consolidatedSummary =
        new([], 0, 0, 0, 0);

    private GmailMailboxStatusResponse gmailStatus =
        new(false, false, "revestikcr@gmail.com", null, null, null, null, false);

    private ExpenseView activeView = ExpenseView.Overview;
    private VoucherView activeVoucherView = VoucherView.Review;
    private string? errorMessage;
    private string? successMessage;
    private int totalPages = 1;
    private int reviewVoucherTotalPages = 1;
    private int historyVoucherTotalPages = 1;
    private bool isFormOpen;
    private bool isLoading;
    private bool isSaving;
    private bool isVoucherBusy;

    [Inject]
    private IExpenseApiService ExpenseApiService { get; set; } = default!;

    [Inject]
    private IBankVoucherApiService BankVoucherApiService { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    private bool CanGoPrevious => !isLoading && listRequest.Page > 1;
    private bool CanGoNext => !isLoading && listRequest.Page < totalPages;

    private bool CanGoPreviousVoucher =>
        !isVoucherBusy && ActiveVoucherRequest.Page > 1;

    private bool CanGoNextVoucher =>
        !isVoucherBusy &&
        ActiveVoucherRequest.Page < ActiveVoucherTotalPages;

    private BankVoucherListRequest ActiveVoucherRequest =>
        activeVoucherView == VoucherView.Review
            ? reviewVoucherRequest
            : historyVoucherRequest;

    private int ActiveVoucherTotalPages =>
        activeVoucherView == VoucherView.Review
            ? reviewVoucherTotalPages
            : historyVoucherTotalPages;

    private IReadOnlyList<BankVoucherReviewItemResponse> VisibleVouchers =>
        activeVoucherView == VoucherView.Review
            ? reviewVouchers
            : historyVouchers;

    protected override async Task OnInitializedAsync()
    {
        SetCurrentMonth();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (isLoading)
            return;

        errorMessage = null;
        isLoading = true;

        try
        {
            var token = cancellationTokenSource.Token;
            SyncVoucherDateFilters();

            var summaryRequest = new ExpenseSummaryRequest
            {
                DateFrom = listRequest.DateFrom,
                DateTo = listRequest.DateTo
            };

            var pageTask = ExpenseApiService.GetPageAsync(listRequest, token);
            var summaryTask = ExpenseApiService.GetSummaryAsync(summaryRequest, token);
            var consolidatedTask =
                ExpenseApiService.GetConsolidatedSummaryAsync(summaryRequest, token);
            var reviewTask = BankVoucherApiService.GetVouchersAsync(
                reviewVoucherRequest,
                token);
            var historyTask = BankVoucherApiService.GetVouchersAsync(
                historyVoucherRequest,
                token);
            var gmailTask = BankVoucherApiService.GetGmailStatusAsync(token);

            await Task.WhenAll(
                pageTask,
                summaryTask,
                consolidatedTask,
                reviewTask,
                historyTask,
                gmailTask);

            var page = await pageTask;
            expenses = page.Items;
            summary = await summaryTask;
            consolidatedSummary = await consolidatedTask;
            gmailStatus = await gmailTask;

            ApplyReviewPage(await reviewTask);
            ApplyHistoryPage(await historyTask);

            totalPages = Math.Max(
                1,
                (int)Math.Ceiling(
                    page.TotalCount / (double)listRequest.PageSize));
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            errorMessage = exception.Message;
        }
        finally
        {
            isLoading = false;
        }
    }

    private void ToggleForm()
    {
        isFormOpen = !isFormOpen;
        errorMessage = null;
        successMessage = null;

        if (isFormOpen)
            formModel = CreateDefaultForm();
    }

    private void CloseForm()
    {
        isFormOpen = false;
        formModel = CreateDefaultForm();
    }

    private async Task SaveExpenseAsync()
    {
        if (isSaving)
            return;

        errorMessage = null;
        successMessage = null;
        isSaving = true;

        try
        {
            await ExpenseApiService.CreateAsync(
                formModel,
                cancellationTokenSource.Token);

            successMessage = "Gasto registrado correctamente.";
            isFormOpen = false;
            formModel = CreateDefaultForm();
            listRequest.Page = 1;
            await LoadAsync();
        }
        catch (Exception exception)
        {
            errorMessage = exception.Message;
        }
        finally
        {
            isSaving = false;
        }
    }

    private async Task ConnectVoucherGmailAsync()
    {
        isVoucherBusy = true;
        errorMessage = null;

        try
        {
            var url = await BankVoucherApiService.GetAuthorizationUrlAsync(
                cancellationTokenSource.Token);

            Navigation.NavigateTo(url, forceLoad: true);
        }
        catch (Exception exception)
        {
            errorMessage = exception.Message;
            isVoucherBusy = false;
        }
    }

    private async Task SyncVouchersAsync()
    {
        isVoucherBusy = true;
        errorMessage = null;
        successMessage = null;

        try
        {
            var result = await BankVoucherApiService.SyncAsync(
                cancellationTokenSource.Token);

            successMessage =
                $"Sincronización completa: {result.Imported} importado(s), " +
                $"{result.Duplicates} duplicado(s), " +
                $"{result.Unrecognized} no reconocido(s), " +
                $"{result.Failed} fallo(s).";

            reviewVoucherRequest.Page = 1;
            historyVoucherRequest.Page = 1;
            await LoadAsync();
        }
        catch (Exception exception)
        {
            errorMessage = exception.Message;
        }
        finally
        {
            isVoucherBusy = false;
        }
    }

    private async Task DisconnectVoucherGmailAsync()
    {
        isVoucherBusy = true;
        errorMessage = null;

        try
        {
            await BankVoucherApiService.DisconnectAsync(
                cancellationTokenSource.Token);

            successMessage = "Gmail de vouchers desconectado.";
            await LoadAsync();
        }
        catch (Exception exception)
        {
            errorMessage = exception.Message;
        }
        finally
        {
            isVoucherBusy = false;
        }
    }

    private Task AcceptVoucherAsync(int id) =>
        ExecuteVoucherActionAsync(
            () => BankVoucherApiService.AcceptAsync(
                id,
                cancellationTokenSource.Token),
            "Voucher aceptado.");

    private Task IgnoreVoucherAsync(int id) =>
        ExecuteVoucherActionAsync(
            () => BankVoucherApiService.IgnoreAsync(
                id,
                cancellationTokenSource.Token),
            "Voucher ignorado.");

    private Task MatchVoucherAsync(
        int id,
        int electronicDocumentId) =>
        ExecuteVoucherActionAsync(
            () => BankVoucherApiService.MatchAsync(
                id,
                electronicDocumentId,
                cancellationTokenSource.Token),
            "Voucher vinculado con la factura electrónica.");

    private async Task ExecuteVoucherActionAsync(
        Func<Task> action,
        string success)
    {
        if (isVoucherBusy)
            return;

        isVoucherBusy = true;
        errorMessage = null;
        successMessage = null;

        try
        {
            await action();
            successMessage = success;
            await RefreshVoucherDataAsync();

            if (activeVoucherView == VoucherView.Review &&
                reviewVouchers.Count == 0 &&
                reviewVoucherRequest.Page > 1)
            {
                reviewVoucherRequest.Page--;
                await RefreshVoucherDataAsync();
            }
        }
        catch (Exception exception)
        {
            errorMessage = exception.Message;
        }
        finally
        {
            isVoucherBusy = false;
        }
    }

    private async Task RefreshVoucherDataAsync()
    {
        var token = cancellationTokenSource.Token;
        SyncVoucherDateFilters();

        var summaryRequest = new ExpenseSummaryRequest
        {
            DateFrom = listRequest.DateFrom,
            DateTo = listRequest.DateTo
        };

        var consolidatedTask =
            ExpenseApiService.GetConsolidatedSummaryAsync(
                summaryRequest,
                token);

        var reviewTask =
            BankVoucherApiService.GetVouchersAsync(
                reviewVoucherRequest,
                token);

        var historyTask =
            BankVoucherApiService.GetVouchersAsync(
                historyVoucherRequest,
                token);

        await Task.WhenAll(
            consolidatedTask,
            reviewTask,
            historyTask);

        consolidatedSummary = await consolidatedTask;
        ApplyReviewPage(await reviewTask);
        ApplyHistoryPage(await historyTask);
    }

    private void ApplyReviewPage(BankVoucherPageResponse page)
    {
        reviewVouchers = page.Items;
        reviewVoucherTotalPages = CalculateTotalPages(
            page.TotalCount,
            page.PageSize);
    }

    private void ApplyHistoryPage(BankVoucherPageResponse page)
    {
        historyVouchers = page.Items;
        historyVoucherTotalPages = CalculateTotalPages(
            page.TotalCount,
            page.PageSize);
    }

    private static int CalculateTotalPages(
        int totalCount,
        int pageSize) =>
        Math.Max(
            1,
            (int)Math.Ceiling(
                totalCount / (double)pageSize));

    private async Task SelectVoucherViewAsync(VoucherView view)
    {
        if (activeVoucherView == view)
            return;

        activeVoucherView = view;
        errorMessage = null;
        successMessage = null;

        await RefreshActiveVoucherPageAsync();
    }

    private async Task PreviousVoucherPageAsync()
    {
        if (!CanGoPreviousVoucher)
            return;

        ActiveVoucherRequest.Page--;
        await RefreshActiveVoucherPageAsync();
    }

    private async Task NextVoucherPageAsync()
    {
        if (!CanGoNextVoucher)
            return;

        ActiveVoucherRequest.Page++;
        await RefreshActiveVoucherPageAsync();
    }

    private async Task RefreshActiveVoucherPageAsync()
    {
        if (isVoucherBusy)
            return;

        isVoucherBusy = true;

        try
        {
            SyncVoucherDateFilters();

            var page = await BankVoucherApiService.GetVouchersAsync(
                ActiveVoucherRequest,
                cancellationTokenSource.Token);

            if (activeVoucherView == VoucherView.Review)
                ApplyReviewPage(page);
            else
                ApplyHistoryPage(page);
        }
        catch (Exception exception)
        {
            errorMessage = exception.Message;
        }
        finally
        {
            isVoucherBusy = false;
        }
    }

    private async Task ApplyFiltersAsync()
    {
        listRequest.Page = 1;
        reviewVoucherRequest.Page = 1;
        historyVoucherRequest.Page = 1;
        await LoadAsync();
    }

    private async Task ClearFiltersAsync()
    {
        listRequest.Search = null;
        listRequest.Page = 1;
        reviewVoucherRequest.Page = 1;
        historyVoucherRequest.Page = 1;
        SetCurrentMonth();
        await LoadAsync();
    }

    private async Task PreviousPageAsync()
    {
        if (!CanGoPrevious)
            return;

        listRequest.Page--;
        await LoadAsync();
    }

    private async Task NextPageAsync()
    {
        if (!CanGoNext)
            return;

        listRequest.Page++;
        await LoadAsync();
    }

    private void SyncVoucherDateFilters()
    {
        reviewVoucherRequest.DateFrom = listRequest.DateFrom;
        reviewVoucherRequest.DateTo = listRequest.DateTo;
        historyVoucherRequest.DateFrom = listRequest.DateFrom;
        historyVoucherRequest.DateTo = listRequest.DateTo;
    }

    private void SetCurrentMonth()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var firstDay = new DateOnly(today.Year, today.Month, 1);
        listRequest.DateFrom = firstDay;
        listRequest.DateTo = firstDay.AddMonths(1).AddDays(-1);
        SyncVoucherDateFilters();
    }

    private string GetGmailStatusLabel() =>
        gmailStatus.IsConnected
            ? "Conectado"
            : gmailStatus.IsConfigured
                ? "No conectado"
                : "Sin credenciales";

    private static string FormatSyncDate(DateTime? value) =>
        value.HasValue
            ? value.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm")
            : "Aún no realizada";

    private static string FormatMoney(string currency, decimal amount) =>
        currency.Equals("CRC", StringComparison.OrdinalIgnoreCase)
            ? $"₡ {amount:N2}"
            : $"{currency} {amount:N2}";

    private static string GetVoucherStatusLabel(BankVoucherStatus status) =>
        status switch
        {
            BankVoucherStatus.Accepted => "Aceptado",
            BankVoucherStatus.NeedsReview => "Por revisar",
            BankVoucherStatus.Matched => "Vinculado",
            BankVoucherStatus.Ignored => "Ignorado",
            _ => status.ToString()
        };

    private static string GetVoucherStatusClass(BankVoucherStatus status) =>
        status switch
        {
            BankVoucherStatus.Accepted => "status-accepted",
            BankVoucherStatus.NeedsReview => "status-review",
            BankVoucherStatus.Matched => "status-matched",
            BankVoucherStatus.Ignored => "status-ignored",
            _ => string.Empty
        };

    private static ExpenseCreateRequest CreateDefaultForm() =>
        new()
        {
            ExpenseDate = DateOnly.FromDateTime(DateTime.Today)
        };

    public void Dispose()
    {
        cancellationTokenSource.Cancel();
        cancellationTokenSource.Dispose();
    }

    private enum ExpenseView
    {
        Overview,
        Manual,
        Vouchers
    }

    private enum VoucherView
    {
        Review,
        History
    }
}
