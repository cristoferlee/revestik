namespace Revestik.Api.Authorization;

public static class RoleNames
{
    public const string Administrator = "Administrator";

    public const string Accountant = "Accountant";

    public const string Sales = "Sales";

    public const string Warehouse = "Warehouse";

    public static readonly string[] All =
    [
        Administrator,
        Accountant,
        Sales,
        Warehouse
    ];

    public static readonly string[] Obsolete =
    [
        "Staff"
    ];
}