namespace Revestik.Api.Services.ElectronicDocuments;

public sealed class ReceivedDocumentInboxException(string message)
    : Exception(message);
