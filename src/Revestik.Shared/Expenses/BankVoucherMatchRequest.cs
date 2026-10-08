using System.ComponentModel.DataAnnotations;

namespace Revestik.Shared.Expenses;

public sealed class BankVoucherMatchRequest
{
    [Range(1, int.MaxValue)]
    public int ElectronicDocumentId { get; set; }
}
