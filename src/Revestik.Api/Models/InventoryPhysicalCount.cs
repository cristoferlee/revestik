using Revestik.Api.Models.Identity;
using Revestik.Shared.Inventory;

namespace Revestik.Api.Models;

public sealed class InventoryPhysicalCount
{
    public int Id { get; set; }

    public PhysicalCountStatus Status { get; set; }

    public string Notes { get; set; } = string.Empty;

    public string StartedByUserId { get; set; } = string.Empty;

    public ApplicationUser StartedByUser { get; set; } = null!;

    public DateTime StartedAtUtc { get; set; }

    public string? CompletedByUserId { get; set; }

    public ApplicationUser? CompletedByUser { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public ICollection<InventoryPhysicalCountLine> Lines { get; set; } = [];

    public ICollection<InventoryMovement> Movements { get; set; } = [];
}