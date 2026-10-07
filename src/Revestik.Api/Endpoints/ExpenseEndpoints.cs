using System.ComponentModel.DataAnnotations;
using Revestik.Api.Authorization;
using Revestik.Api.Services.Expenses;
using Revestik.Shared.Expenses;

namespace Revestik.Api.Endpoints;

public static class ExpenseEndpoints
{
    public static IEndpointRouteBuilder MapExpenseEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/api/expenses")
            .WithTags("Expenses")
            .RequireAuthorization(PolicyNames.ManageExpenses);

        group.MapGet(
            "/",
            async (
                [AsParameters] ExpenseListRequest request,
                IExpenseService expenseService,
                CancellationToken cancellationToken) =>
            {
                var validationErrors = ValidateRequest(request);

                if (validationErrors.Count > 0)
                {
                    return Results.ValidationProblem(validationErrors);
                }

                var expenses = await expenseService.GetPageAsync(
                    request,
                    cancellationToken);

                return Results.Ok(expenses);
            })
            .WithName("GetExpenses");

        group.MapGet(
            "/summary",
            async (
                [AsParameters] ExpenseSummaryRequest request,
                IExpenseService expenseService,
                CancellationToken cancellationToken) =>
            {
                var validationErrors = ValidateRequest(request);

                if (validationErrors.Count > 0)
                {
                    return Results.ValidationProblem(validationErrors);
                }

                var summary = await expenseService.GetSummaryAsync(
                    request,
                    cancellationToken);

                return Results.Ok(summary);
            })
            .WithName("GetExpenseSummary");

        group.MapPost(
            "/",
            async (
                ExpenseCreateRequest request,
                IExpenseService expenseService,
                CancellationToken cancellationToken) =>
            {
                var validationErrors = ValidateRequest(request);

                if (validationErrors.Count > 0)
                {
                    return Results.ValidationProblem(validationErrors);
                }

                var expense = await expenseService.CreateAsync(
                    request,
                    cancellationToken);

                return Results.Created(
                    $"/api/expenses/{expense.Id}",
                    expense);
            })
            .AddEndpointFilter<AntiforgeryValidationFilter>()
            .WithName("CreateExpense");

        return endpoints;
    }

    private static Dictionary<string, string[]> ValidateRequest<TRequest>(
        TRequest request)
        where TRequest : class
    {
        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(request);

        Validator.TryValidateObject(
            request,
            validationContext,
            validationResults,
            validateAllProperties: true);

        return validationResults
            .SelectMany(result =>
                result.MemberNames
                    .DefaultIfEmpty("request")
                    .Select(memberName => new
                    {
                        MemberName = memberName,
                        ErrorMessage =
                            result.ErrorMessage ?? "Invalid value."
                    }))
            .GroupBy(error => error.MemberName)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(error => error.ErrorMessage)
                    .ToArray());
    }
}
