namespace Revestik.Api.Authorization;

public static class PolicyNames
{
    public const string AdministratorOnly =
        "AdministratorOnly";

    public const string ManageCustomers =
        "ManageCustomers";

    public const string ManageInventory =
        "ManageInventory";

    public const string ManageInvoices =
        "ManageInvoices";

    public const string ViewAuditLog =
        "ViewAuditLog";
}