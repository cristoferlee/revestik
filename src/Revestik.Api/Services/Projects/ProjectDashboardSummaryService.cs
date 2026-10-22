using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Shared.Projects;

namespace Revestik.Api.Services.Projects;

// This read-only snapshot never alters projects, payments or sales.
public sealed class ProjectDashboardSummaryService(RevestikDbContext db)
{
    public async Task<ProjectDashboardSummaryResponse> GetAsync(CancellationToken ct)
    {
        var projects = await db.Set<Project>()
            .AsNoTracking()
            .Select(p => new
            {
                p.Id,
                p.Status,
                p.AgreedAmount,
                p.CurrencyCode,
                Paid = db.Set<ProjectPayment>()
                    .Where(payment => payment.ProjectId == p.Id && payment.Status == "Active")
                    .Sum(payment => (decimal?)payment.Amount) ?? 0m
            })
            .ToListAsync(ct);

        var open = projects.Where(p => p.Status is ProjectStatus.Planning
            or ProjectStatus.PendingApproval or ProjectStatus.Active or ProjectStatus.Paused).ToList();

        var currencies = new[] { "CRC", "USD" }.Select(currency =>
        {
            var items = open.Where(p => p.AgreedAmount.HasValue &&
                string.Equals(p.CurrencyCode, currency, StringComparison.OrdinalIgnoreCase));
            return new ProjectDashboardCurrencySummaryResponse(
                currency,
                decimal.Round(items.Sum(p => p.AgreedAmount!.Value), 2, MidpointRounding.AwayFromZero),
                decimal.Round(items.Sum(p => p.Paid), 2, MidpointRounding.AwayFromZero),
                decimal.Round(items.Sum(p => p.AgreedAmount!.Value - p.Paid), 2, MidpointRounding.AwayFromZero));
        }).ToList();

        return new ProjectDashboardSummaryResponse(
            projects.Count,
            open.Count,
            projects.Count(p => p.Status == ProjectStatus.Completed),
            projects.Count(p => p.Status == ProjectStatus.Cancelled),
            projects.Count(p => p.Status == ProjectStatus.Archived),
            projects.Count(p => p.Status == ProjectStatus.Planning),
            projects.Count(p => p.Status == ProjectStatus.PendingApproval),
            projects.Count(p => p.Status == ProjectStatus.Active),
            projects.Count(p => p.Status == ProjectStatus.Paused),
            open.Count(p => !p.AgreedAmount.HasValue),
            projects.Count(p => p.Status == ProjectStatus.Completed &&
                p.AgreedAmount.HasValue && p.AgreedAmount.Value > p.Paid),
            currencies);
    }
}
