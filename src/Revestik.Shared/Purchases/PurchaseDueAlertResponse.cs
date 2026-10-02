namespace Revestik.Shared.Purchases;

public sealed record PurchaseDueAlertResponse(
    int ShortWindowDays,
    int LongWindowDays,
    IReadOnlyList<PurchaseDueAlertItemResponse> Items);