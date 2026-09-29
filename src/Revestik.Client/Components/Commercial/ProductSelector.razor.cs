using Microsoft.AspNetCore.Components;
using Revestik.Client.Services.Products;
using Revestik.Shared.Products;

namespace Revestik.Client.Components.Commercial;

public partial class ProductSelector : ComponentBase, IDisposable
{
    private readonly CancellationTokenSource cancellationTokenSource = new();
    private IReadOnlyList<ProductListItemResponse> productSearchResults = [];
    private string productSearchTerm = string.Empty;
    private string? errorMessage;
    private bool isSearchingProducts;
    private bool hasSearchedProducts;

    [Parameter] public EventCallback<ProductListItemResponse> ProductSelected { get; set; }
    [Parameter] public EventCallback OnClose { get; set; }
    [Inject] private IProductApiService ProductApiService { get; set; } = default!;

    protected override async Task OnInitializedAsync() => await SearchProductsAsync();

    private async Task SearchProductsAsync()
    {
        errorMessage = null;
        hasSearchedProducts = true;
        isSearchingProducts = true;

        try
        {
            var result = await ProductApiService.GetPageAsync(
                new ProductListRequest
                {
                    Search = string.IsNullOrWhiteSpace(productSearchTerm)
                        ? null
                        : productSearchTerm.Trim(),
                    ActivityStatus = ProductActivityStatus.Active,
                    StockStatus = ProductStockStatus.All,
                    SortBy = ProductSortField.Name,
                    SortDirection = SortDirection.Asc,
                    Page = 1,
                    PageSize = 20
                },
                cancellationTokenSource.Token);

            productSearchResults = result.Items;
        }
        catch (OperationCanceledException) { }
        catch (HttpRequestException)
        {
            errorMessage = "No fue posible cargar los productos. Verifica la conexión con la API.";
        }
        finally
        {
            isSearchingProducts = false;
        }
    }

    private async Task ClearSearchAsync()
    {
        productSearchTerm = string.Empty;
        await SearchProductsAsync();
    }

    private async Task SelectProductAsync(ProductListItemResponse product) =>
        await ProductSelected.InvokeAsync(product);

    private async Task CloseAsync() => await OnClose.InvokeAsync();

    public void Dispose()
    {
        cancellationTokenSource.Cancel();
        cancellationTokenSource.Dispose();
    }
}