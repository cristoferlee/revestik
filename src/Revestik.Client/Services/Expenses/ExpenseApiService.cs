using System.Globalization;
using System.Net.Http.Json;
using Revestik.Shared.Common;
using Revestik.Shared.Expenses;

namespace Revestik.Client.Services.Expenses;

public sealed class ExpenseApiService(HttpClient httpClient)
    : IExpenseApiService
{
    public async Task<PaginatedResponse<ExpenseListItemResponse>> GetPageAsync(
        ExpenseListRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync(
            BuildListUrl(request),
            cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<PaginatedResponse<ExpenseListItemResponse>>(
                cancellationToken)
            ?? new PaginatedResponse<ExpenseListItemResponse>(
                [],
                request.Page,
                request.PageSize,
                0);
    }

    public async Task<ExpenseSummaryResponse> GetSummaryAsync(
        ExpenseSummaryRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync(
            BuildSummaryUrl(request),
            cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<ExpenseSummaryResponse>(
                cancellationToken)
            ?? new ExpenseSummaryResponse(0, 0m);
    }

    public async Task<ExpenseResponse> CreateAsync(
        ExpenseCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync(
            "api/expenses",
            request,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<ExpenseResponse>(
                cancellationToken)
            ?? throw new InvalidOperationException(
                "The API returned an empty expense response.");
    }

    private static string BuildListUrl(ExpenseListRequest request)
    {
        var parameters = new List<string>();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            Add(parameters, "Search", request.Search.Trim());
        }

        AddDate(parameters, "DateFrom", request.DateFrom);
        AddDate(parameters, "DateTo", request.DateTo);
        Add(
            parameters,
            "Page",
            request.Page.ToString(CultureInfo.InvariantCulture));
        Add(
            parameters,
            "PageSize",
            request.PageSize.ToString(CultureInfo.InvariantCulture));

        return $"api/expenses?{string.Join("&", parameters)}";
    }

    private static string BuildSummaryUrl(ExpenseSummaryRequest request)
    {
        var parameters = new List<string>();
        AddDate(parameters, "DateFrom", request.DateFrom);
        AddDate(parameters, "DateTo", request.DateTo);

        return parameters.Count == 0
            ? "api/expenses/summary"
            : $"api/expenses/summary?{string.Join("&", parameters)}";
    }

    private static void AddDate(
        ICollection<string> parameters,
        string name,
        DateOnly? value)
    {
        if (!value.HasValue)
        {
            return;
        }

        Add(
            parameters,
            name,
            value.Value.ToString(
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture));
    }

    private static void Add(
        ICollection<string> parameters,
        string name,
        string value)
    {
        parameters.Add(
            $"{Uri.EscapeDataString(name)}=" +
            $"{Uri.EscapeDataString(value)}");
    }
}
