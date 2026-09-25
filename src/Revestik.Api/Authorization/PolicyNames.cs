namespace Revestik.Api.Authorization;

public static class PolicyNames
{
    public const string AdministratorOnly =
        "AdministratorOnly";

    public const string ManageCustomers =
        "ManageCustomers";

    public const string ManageInventory =
        "ManageInventory";

    public const string ManageProductCatalog =
        "ManageProductCatalog";

    public const string ManageSales =
        "ManageSales";

    public const string ManageQuotations =
        "ManageQuotations";

    public const string ViewAuditLog =
        "ViewAuditLog";
}