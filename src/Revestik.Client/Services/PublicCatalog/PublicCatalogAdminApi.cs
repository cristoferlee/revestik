using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Forms;

namespace Revestik.Client.Services.PublicCatalog;

// Uses the existing cookie + CSRF HttpClient registered in Revestik.Client/Program.cs.
public sealed class PublicCatalogAdminApi(HttpClient http)
{
    public string StagedImageUri(string token) => new Uri(http.BaseAddress ?? throw new InvalidOperationException("API base URL missing."), $"api/admin/public-catalog/images/staged/{Uri.EscapeDataString(token)}").ToString();
    public string ImageUri(int id) => new Uri(http.BaseAddress ?? throw new InvalidOperationException("API base URL missing."), $"api/admin/public-catalog/images/{id}/content").ToString();
    public Task<List<AdminCategory>> Categories() => Read<List<AdminCategory>>("api/admin/public-catalog/categories");
    public Task<List<AdminSubcategory>> Subcategories() => Read<List<AdminSubcategory>>("api/admin/public-catalog/subcategories");
    public async Task<List<AdminProductSummary>> Products()
    {
        var page = await Read<AdminProductPage>("api/admin/public-catalog/products?page=1&pageSize=50");
        return page.Items;
    }
    public Task<AdminProductPage> ProductPage(int page) => Read<AdminProductPage>($"api/admin/public-catalog/products?page={Math.Max(1, page)}&pageSize=24");
    public Task<ProductOperationResult> SaveContent(int id, AdminProductWrite product, List<AdminStageAttachment> pending,
        List<AdminImage> existing, IEnumerable<int> removed) =>
        Send<ProductOperationResult>(HttpMethod.Put, $"api/admin/public-catalog/products/{id}/content",
            new
            {
                Product = product,
                PendingImages = pending,
                ExistingImages = existing.Select(x => new { x.Id, x.DisplayOrder, x.AltEs, x.AltEn, x.AltDe, x.Version }),
                RemovedImageIds = removed.ToArray()
            });
    public Task<AdminProduct> Product(int id) => Read<AdminProduct>($"api/admin/public-catalog/products/{id}");
    public async Task<bool> StagedExists(string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"api/admin/public-catalog/images/staged/{Uri.EscapeDataString(token)}");
        // Only inspect response headers; do not download the image to the Blazor client.
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        if (response.StatusCode == HttpStatusCode.NotFound) return false;
        await Check(response);
        return true;
    }

    public Task<AdminCategory> SaveCategory(AdminCategory c) => c.Id == 0
        ? Send<AdminCategory>(HttpMethod.Post, "api/admin/public-catalog/categories", c.Write())
        : Send<AdminCategory>(HttpMethod.Put, $"api/admin/public-catalog/categories/{c.Id}", c.Write());
    public Task<AdminSubcategory> SaveSubcategory(AdminSubcategory c) => c.Id == 0
        ? Send<AdminSubcategory>(HttpMethod.Post, "api/admin/public-catalog/subcategories", c.Write())
        : Send<AdminSubcategory>(HttpMethod.Put, $"api/admin/public-catalog/subcategories/{c.Id}", c.Write());
    public Task<AdminProduct> CreateProduct(AdminProductWrite p) => Send<AdminProduct>(HttpMethod.Post,"api/admin/public-catalog/products", p);
    public Task<AdminProduct> UpdateProduct(int id,AdminProductWrite p) => Send<AdminProduct>(HttpMethod.Put,$"api/admin/public-catalog/products/{id}",p);
    public async Task DeleteProduct(int id, string version)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete,
            $"api/admin/public-catalog/products/{id}?version={Uri.EscapeDataString(version)}");
        using var response = await http.SendAsync(request);
        await Check(response);
    }
    public Task<ProductOperationResult> Publish(int id,string version,bool publish) =>
        Send<ProductOperationResult>(HttpMethod.Post,$"api/admin/public-catalog/products/{id}/{(publish ? "publish" : "unpublish")}",new { Version=version });
    public async Task<StagedImage> Stage(IBrowserFile file,string role)
    {
        if(file.Size is <=0 or >5242880)throw new InvalidOperationException("La imagen debe medir 5 MB como máximo.");
        using var multipart=new MultipartFormDataContent();
        using var stream=new StreamContent(file.OpenReadStream(5242880));
        // The browser-provided MIME type is advisory; the API validates the actual bytes.
        if (!System.Net.Http.Headers.MediaTypeHeaderValue.TryParse(file.ContentType, out var contentType))
            contentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        stream.Headers.ContentType = contentType;
        multipart.Add(stream,"file",file.Name);
        multipart.Add(new StringContent(role),"role");
        using var response=await http.PostAsync("api/admin/public-catalog/images/stage",multipart);
        await Check(response);
        return await response.Content.ReadFromJsonAsync<StagedImage>() ?? throw new InvalidOperationException("El servidor no devolvió un identificador de imagen.");
    }
    public Task<ImageAttachmentResult> AttachProduct(int productId,AdminImageAttach attach) =>
        Send<ImageAttachmentResult>(HttpMethod.Post,$"api/admin/public-catalog/products/{productId}/images",attach);
    public Task<ImageAttachmentResult> AttachCategory(int categoryId,AdminImageAttach attach) =>
        Send<ImageAttachmentResult>(HttpMethod.Post,$"api/admin/public-catalog/categories/{categoryId}/images",attach);
    public Task<AdminImage> SaveImage(AdminImage image) => Send<AdminImage>(HttpMethod.Put,$"api/admin/public-catalog/images/{image.Id}",new{image.DisplayOrder,image.AltEs,image.AltEn,image.AltDe,image.Version});
    public async Task DeleteImage(AdminImage image)
    {
        using var request=new HttpRequestMessage(HttpMethod.Delete,$"api/admin/public-catalog/images/{image.Id}?version={Uri.EscapeDataString(image.Version)}");
        using var response=await http.SendAsync(request);await Check(response);
    }
    public async Task Discard(string token)
    {
        using var response=await http.DeleteAsync($"api/admin/public-catalog/images/staged/{Uri.EscapeDataString(token)}");
        await Check(response);
    }
    public async Task<T> Read<T>(string uri) => await http.GetFromJsonAsync<T>(uri) ?? throw new InvalidOperationException("Respuesta vacía del servidor.");
    private async Task<T> Send<T>(HttpMethod method,string uri,object value)
    {
        using var request=new HttpRequestMessage(method,uri){Content=JsonContent.Create(value)};
        using var response=await http.SendAsync(request);
        await Check(response);
        return await response.Content.ReadFromJsonAsync<T>() ?? throw new InvalidOperationException("Respuesta vacía del servidor.");
    }
    private static async Task Check(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;

        // Only display messages from a known, bounded set of API validation responses.
        // Never display arbitrary server exceptions, stack traces or HTML.
        var apiMessage = await KnownApiMessage(response);
        var description = response.StatusCode switch
        {
            HttpStatusCode.BadRequest when apiMessage == "Invalid CSRF token." =>
                "La sesión de edición necesita renovarse. Recargá la página e iniciá sesión nuevamente si es necesario.",
            HttpStatusCode.BadRequest => apiMessage switch
            {
                "Maximum image size is 5 MB." or "Image must be between 1 byte and 5 MB." =>
                    "La imagen supera el límite de 5 MB o está vacía.",
                "Extension and image content do not match." or "Unsupported image signature." or
                "Unsupported image format." or "Adjuntá una imagen PNG, JPG o WebP válida." =>
                    "La fotografía no es un JPG, PNG o WebP válido, o su extensión no coincide con el contenido.",
                "Image dimensions are outside safety limits." =>
                    "La resolución de la fotografía supera los límites permitidos.",
                "Staged image is missing or invalid." or "Uploaded image is missing or invalid." =>
                    "Una fotografía temporal ya no está disponible. Quitala o volvé a subirla antes de guardar.",
                "Invalid product fields or subcategories." or "Invalid product, category, or unit selection." =>
                    "Revisá el nombre, descripción, subcategorías, precio y unidad de venta.",
                "Invalid gallery changes." or "A published product requires one cover and at most five images." or
                "Publish requires one cover and at most five valid staged images." =>
                    "Revisá las fotografías: debe haber una portada y un máximo de cinco imágenes.",
                _ => "El servidor rechazó la operación (HTTP 400). Revisá los campos o la sesión de edición."
            },
            HttpStatusCode.Conflict when apiMessage == "Product or images changed. Reload before deleting." =>
                "El producto cambió en otra sesión o alguna imagen sigue en uso. Recargá antes de eliminar.",
            HttpStatusCode.Conflict when apiMessage == "Product is referenced by another record." =>
                "El producto tiene referencias que impiden eliminarlo. Recargá y revisá el contenido.",
            HttpStatusCode.Conflict => apiMessage == "Gallery changed. Reload before saving." ||
                apiMessage == "Product or gallery changed. Reload." || apiMessage == "Product changed. Reload."
                    ? "El producto o sus fotografías cambiaron en otra sesión. Conservá tus datos y recargá antes de volver a guardar."
                    : "Los datos cambiaron en otra sesión o entran en conflicto. Revisá antes de intentarlo nuevamente.",
            HttpStatusCode.NotFound =>
                "El recurso solicitado ya no existe o no está disponible. Revisá la fotografía o recargá los datos.",
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                "La sesión expiró o no tenés autorización. Iniciá sesión nuevamente.",
            HttpStatusCode.RequestEntityTooLarge => "La fotografía excede el tamaño permitido de 5 MB.",
            HttpStatusCode.ServiceUnavailable or HttpStatusCode.BadGateway =>
                "El servicio no está disponible en este momento. Revisá el estado antes de reintentar.",
            _ => $"La operación falló (HTTP {(int)response.StatusCode})."
        };
        throw new InvalidOperationException(description);
    }

    private static async Task<string?> KnownApiMessage(HttpResponseMessage response)
    {
        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (mediaType is not ("application/json" or "application/problem+json")) return null;
        var body = await response.Content.ReadAsStringAsync();
        if (body.Length > 4096) return null;
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return null;
            foreach (var field in new[] { "message", "title" })
            {
                if (document.RootElement.TryGetProperty(field, out var value) &&
                    value.ValueKind == JsonValueKind.String)
                    return value.GetString();
            }
        }
        catch (JsonException) { /* Unknown response: use generic status text. */ }
        return null;
    }
}

public sealed class AdminCategory
{
    public int Id {get;set;}
    public string Slug {get;set;}="";
    public string NameEs {get;set;}="";
    public string? NameEn {get;set;}
    public string? NameDe {get;set;}
    public string? DescriptionEs {get;set;}
    public string? DescriptionEn {get;set;}
    public string? DescriptionDe {get;set;}
    public string? HeroTitleEs {get;set;}
    public string? HeroTitleEn {get;set;}
    public string? HeroTitleDe {get;set;}
    public string? HeroDescriptionEs {get;set;}
    public string? HeroDescriptionEn {get;set;}
    public string? HeroDescriptionDe {get;set;}
    public int DisplayOrder {get;set;}
    public bool IsActive {get;set;}=true;
    public string Version {get;set;}="";
    public byte[]? RowVersion {get;set;}
    public object Write()=>new{Slug,NameEs,NameEn,NameDe,DescriptionEs,DescriptionEn,DescriptionDe,HeroTitleEs,HeroTitleEn,HeroTitleDe,HeroDescriptionEs,HeroDescriptionEn,HeroDescriptionDe,DisplayOrder,IsActive,Version=Id==0?null:VersionFromRow()};
    public string VersionFromRow()=>RowVersion is {Length:>0}?Convert.ToBase64String(RowVersion):Version;
}
public sealed class AdminSubcategory
{
    public int Id {get;set;}
    public int CategoryId {get;set;}
    public string Slug {get;set;}="";
    public string NameEs {get;set;}="";
    public string? NameEn {get;set;}
    public string? NameDe {get;set;}
    public string? DescriptionEs {get;set;}
    public string? DescriptionEn {get;set;}
    public string? DescriptionDe {get;set;}
    public int DisplayOrder {get;set;}
    public bool IsActive {get;set;}=true;
    public string Version {get;set;}="";
    public byte[]? RowVersion {get;set;}
    public object Write()=>new{CategoryId,Slug,NameEs,NameEn,NameDe,DescriptionEs,DescriptionEn,DescriptionDe,DisplayOrder,IsActive,Version=Id==0?null:VersionFromRow()};
    public string VersionFromRow()=>RowVersion is {Length:>0}?Convert.ToBase64String(RowVersion):Version;
}
public sealed class AdminProductPage { public List<AdminProductSummary> Items {get;set;}=[]; public int Total {get;set;} }
public sealed class AdminProductSummary
{
    public int Id {get;set;}
    public string Slug {get;set;}="";
    public bool IsPublished {get;set;}
    public bool IsArchived {get;set;}
    public string? NameEs {get;set;}
    public string? NameEn {get;set;}
    public string? NameDe {get;set;}
    public string? Brand {get;set;}
    public decimal? Price {get;set;}
    public string? Currency {get;set;}
    public string? SalesUnit {get;set;}
    public int? CoverImageId {get;set;}
    public string Name(string language) => language switch
    {
        "en" => NameEn ?? NameEs ?? Slug,
        "de" => NameDe ?? NameEs ?? Slug,
        _ => NameEs ?? Slug
    };
}
public sealed class AdminProduct
{
    public int Id {get;set;}
    public string Slug {get;set;}="";
    public string? Brand {get;set;}
    public decimal? Price {get;set;}
    public string? Currency {get;set;}
    public string? SalesUnit {get;set;}
    public int DisplayOrder {get;set;}
    public bool IsPublished {get;set;}
    public string Version {get;set;}="";
    public List<int> SubcategoryIds {get;set;}=[];
    public List<AdminTranslation> Translations {get;set;}=[];
    public List<AdminImage> Images {get;set;}=[];
    public AdminProductWrite Write()=>new(){Slug=Slug,Brand=Brand,Price=Price,Currency=Currency,SalesUnit=SalesUnit,DisplayOrder=DisplayOrder,Version=Version,SubcategoryIds=SubcategoryIds,Translations=Translations};
}
public sealed class AdminProductWrite
{
    public string Slug {get;set;}="";
    public string? Brand {get;set;}
    public decimal? Price {get;set;}
    public string? Currency {get;set;}
    public string? SalesUnit {get;set;}
    public int DisplayOrder {get;set;}
    public string? Version {get;set;}
    public List<int> SubcategoryIds {get;set;}=[];
    public List<AdminTranslation> Translations {get;set;}=[];
    public List<AdminStageAttachment> PendingImages {get;set;}=[];
}
public sealed class AdminTranslation
{
    public string LanguageCode {get;set;}="es";
    public string Name {get;set;}="";
    public string? Description {get;set;}
    public string? SeoTitle {get;set;}
    public string? SeoDescription {get;set;}
}
public sealed class StagedImage {public string Token {get;set;}="";public string? ContentType {get;set;}public int Width {get;set;}public int Height {get;set;} }
public sealed class AdminStageAttachment
{
    public string Token {get;set;}="";
    public string Role {get;set;}="ProductGallery";
    public int DisplayOrder {get;set;}
    public string? AltEs {get;set;}
    public string? AltEn {get;set;}
    public string? AltDe {get;set;}
}
public sealed class AdminImage
{
    public int Id {get;set;}
    public string Role {get;set;}="";
    public string ContentType {get;set;}="";
    public int DisplayOrder {get;set;}
    public string? AltEs {get;set;}
    public string? AltEn {get;set;}
    public string? AltDe {get;set;}
    public int? FallbackImageId {get;set;}
    public string Version {get;set;}="";
}
public sealed class AdminImageAttach
{
    public string Token {get;set;}="";
    public string Role {get;set;}="";
    public int DisplayOrder {get;set;}
    public string? AltEs {get;set;}
    public string? AltEn {get;set;}
    public string? AltDe {get;set;}
    public int? FallbackImageId {get;set;}
    public string ParentVersion {get;set;}="";
}
public sealed class ImageAttachmentResult {public AdminImage Image {get;set;}=new();public string ParentVersion {get;set;}="";}
public sealed class ProductOperationResult {public int Id {get;set;}public bool IsPublished {get;set;}public string Version {get;set;}="";}
