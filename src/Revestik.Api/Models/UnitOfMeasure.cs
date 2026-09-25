namespace Revestik.Api.Models;

public sealed class UnitOfMeasure
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Symbol { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }
}