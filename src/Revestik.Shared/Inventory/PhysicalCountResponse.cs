namespace Revestik.Shared.Inventory;

public sealed record PhysicalCountResponse(
    int Id,
    PhysicalCountStatus Status,
    string Notes,
    string StartedByUserId,
    DateTime StartedAtUtc,
    string? CompletedByUserId,
    DateTime? CompletedAtUtc,
    IReadOnlyList<PhysicalCountLineResponse> Lines);