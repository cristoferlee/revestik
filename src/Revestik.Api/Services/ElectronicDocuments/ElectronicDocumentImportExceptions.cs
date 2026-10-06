namespace Revestik.Api.Services.ElectronicDocuments;

public sealed class DuplicateElectronicDocumentException(int existingId)
    : Exception("El documento ya fue importado.")
{
    public int ExistingId { get; } = existingId;
}

public sealed class InvalidElectronicDocumentReceiverException()
    : Exception("El documento no está dirigido al receptor fiscal configurado para Revestik.");
