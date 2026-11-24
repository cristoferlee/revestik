namespace Revestik.Api.Authorization;

public static class PolicyNames
{
    // Valid Identity session without employee-role requirements.
    // Only for identity/session operations shared with future public users.
    public const string AuthenticatedUser = "AuthenticatedUser";

    public const string PublicCustomerOnly = "PublicCustomerOnly";

    public const string AdministratorOnly =
        "AdministratorOnly";

    public const string ManageCustomers =
        "ManageCustomers";
    public const string ManageProjects =
        "ManageProjects";

    public const string ManageInventory =
        "ManageInventory";

    public const string ManageProductCatalog =
        "ManageProductCatalog";

    public const string ManageSales =
        "ManageSales";

    public const string ManageQuotations =
        "ManageQuotations";

    public const string ManagePurchases =
        "ManagePurchases";

    public const string ManagePurchasePayments =
        "ManagePurchasePayments";

    public const string ManageExpenses =
        "ManageExpenses";

    public const string ManageAccountingClassification =
        "ManageAccountingClassification";

    public const string ViewAuditLog =
        "ViewAuditLog";
}
