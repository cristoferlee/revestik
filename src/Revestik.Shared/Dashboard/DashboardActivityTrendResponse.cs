namespace Revestik.Shared.Dashboard;

public sealed record DashboardActivityTrendResponse(int Year, int Month, IReadOnlyList<DashboardActivityCurrencyResponse> Currencies);
public sealed record DashboardActivityCurrencyResponse(string Currency, IReadOnlyList<DashboardActivityDayResponse> Days);
public sealed record DashboardActivityDayResponse(int Day, decimal Sales, decimal Collections, decimal InternalPurchases);
