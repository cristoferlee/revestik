namespace Revestik.Api.Services.ElectronicDocuments;

public sealed class ElectronicDocumentXmlException(string message, Exception? innerException = null)
    : Exception(message, innerException);
