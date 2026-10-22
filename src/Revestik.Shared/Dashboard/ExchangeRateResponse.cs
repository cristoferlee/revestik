namespace Revestik.Shared.Dashboard;

public sealed record ExchangeRateResponse(
    decimal PurchaseRate,
    decimal SaleRate,
    DateOnly EffectiveDate,
    bool IsCached,
    bool IsStale);
