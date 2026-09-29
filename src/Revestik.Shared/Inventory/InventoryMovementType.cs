namespace Revestik.Shared.Inventory;

public enum InventoryMovementType
{
    InitialStock = 1,
    AdjustmentIncrease = 2,
    AdjustmentDecrease = 3,
    Sale = 4,
    SaleReversal = 5
}