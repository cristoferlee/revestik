namespace Revestik.Api.Configuration;

public sealed class CompanyOptions
{
    public const string SectionName = "Company";

    public string LegalName { get; set; } = string.Empty;
    public string IdentificationNumber { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Website { get; set; } = string.Empty;
}