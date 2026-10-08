namespace Revestik.Api.Services.GmailIntegration;

public class GmailIntegrationException(string message)
    : Exception(message);

public sealed class GmailSyncAlreadyRunningException()
    : GmailIntegrationException("Ya existe una sincronización de Gmail en curso.");

public sealed class GmailRateLimitException(string message)
    : GmailIntegrationException(message);
