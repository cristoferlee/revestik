namespace Revestik.Api.Services.Company;

// Distinguishes malformed historical identity data from problems reading the logo.
public sealed class CompanyPdfSnapshotException : IOException
{
    public CompanyPdfSnapshotException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
