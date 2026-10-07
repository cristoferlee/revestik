using Microsoft.AspNetCore.Components;
using Revestik.Client.Services.Expenses;
using Revestik.Shared.Expenses;

namespace Revestik.Client.Pages;

public partial class Expenses : ComponentBase, IDisposable
{
    private readonly CancellationTokenSource cancellationTokenSource = new();

    private readonly ExpenseListRequest listRequest = new()
    {
        Page = 1,
        PageSize = 20
    };

    private IReadOnlyList<ExpenseListItemResponse> expenses = [];
    private ExpenseCreateRequest formModel = CreateDefaultForm();
    private ExpenseSummaryResponse summary = new(0, 0m);

    private string? errorMessage;
    private string? successMessage;
    private int totalPages = 1;
    private bool isFormOpen;
    private bool isLoading;
    private bool isSaving;

    [Inject]
    private IExpenseApiService ExpenseApiService { get; set; } = default!;

    private bool CanGoPrevious => !isLoading && listRequest.Page > 1;
    private bool CanGoNext => !isLoading && listRequest.Page < totalPages;

    protected override async Task OnInitializedAsync()
    {
        SetCurrentMonth();
        await LoadAsync();
    }

    private void ToggleForm()
    {
        isFormOpen = !isFormOpen;
        errorMessage = null;
        successMessage = null;

        if (isFormOpen)
        {
            formModel = CreateDefaultForm();
        }
    }

    private void CloseForm()
    {
        isFormOpen = false;
        formModel = CreateDefaultForm();
    }

    private async Task SaveExpenseAsync()
    {
        if (isSaving)
        {
            return;
        }

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
        catch (OperationCanceledException)
        {
        }
        catch (HttpRequestException)
        {
            errorMessage =
                "No fue posible registrar el gasto. Revisa los datos e inténtalo nuevamente.";
        }
        finally
        {
            isSaving = false;
        }
    }

    private async Task ApplyFiltersAsync()
    {
        listRequest.Page = 1;
        await LoadAsync();
    }

    private async Task ClearFiltersAsync()
    {
        listRequest.Search = null;
        listRequest.Page = 1;
        SetCurrentMonth();
        await LoadAsync();
    }

    private async Task PreviousPageAsync()
    {
        if (!CanGoPrevious)
        {
            return;
        }

        listRequest.Page--;
        await LoadPageAsync();
    }

    private async Task NextPageAsync()
    {
        if (!CanGoNext)
        {
            return;
        }

        listRequest.Page++;
        await LoadPageAsync();
    }

    private async Task LoadAsync()
    {
        if (isLoading)
        {
            return;
        }

        errorMessage = null;
        isLoading = true;

        try
        {
            var token = cancellationTokenSource.Token;
            var pageTask = ExpenseApiService.GetPageAsync(listRequest, token);
            var summaryTask = ExpenseApiService.GetSummaryAsync(
                new ExpenseSummaryRequest
                {
                    DateFrom = listRequest.DateFrom,
                    DateTo = listRequest.DateTo
                },
                token);

            await Task.WhenAll(pageTask, summaryTask);

            var page = await pageTask;
            summary = await summaryTask;
            expenses = page.Items;
            totalPages = Math.Max(
                1,
                (int)Math.Ceiling(
                    page.TotalCount / (double)listRequest.PageSize));

            if (listRequest.Page > totalPages)
            {
                listRequest.Page = totalPages;
                await LoadPageCoreAsync(token);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (HttpRequestException)
        {
            errorMessage =
                "No fue posible cargar los gastos. Inténtalo nuevamente.";
        }
        finally
        {
            isLoading = false;
        }
    }

    private async Task LoadPageAsync()
    {
        if (isLoading)
        {
            return;
        }

        errorMessage = null;
        isLoading = true;

        try
        {
            await LoadPageCoreAsync(cancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (HttpRequestException)
        {
            errorMessage =
                "No fue posible cargar los gastos. Inténtalo nuevamente.";
        }
        finally
        {
            isLoading = false;
        }
    }

    private async Task LoadPageCoreAsync(CancellationToken cancellationToken)
    {
        var page = await ExpenseApiService.GetPageAsync(
            listRequest,
            cancellationToken);

        expenses = page.Items;
        totalPages = Math.Max(
            1,
            (int)Math.Ceiling(
                page.TotalCount / (double)listRequest.PageSize));
    }

    private void SetCurrentMonth()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var firstDay = new DateOnly(today.Year, today.Month, 1);
        listRequest.DateFrom = firstDay;
        listRequest.DateTo = firstDay.AddMonths(1).AddDays(-1);
    }

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
}
