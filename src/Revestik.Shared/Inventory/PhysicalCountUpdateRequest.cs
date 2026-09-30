using System.ComponentModel.DataAnnotations;

namespace Revestik.Shared.Inventory;

public sealed class PhysicalCountUpdateRequest
{
    [Required(ErrorMessage = "Debe enviar al menos una línea del conteo.")]
    [MinLength(1, ErrorMessage = "Debe enviar al menos una línea del conteo.")]
    public List<PhysicalCountLineUpdateRequest> Lines { get; set; } = [];
}