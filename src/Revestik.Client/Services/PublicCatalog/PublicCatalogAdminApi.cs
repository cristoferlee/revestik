using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Forms;

namespace Revestik.Client.Services.PublicCatalog;

// Uses the existing cookie + CSRF HttpClient registered in Revestik.Client/Program.cs.
public sealed class PublicCatalogAdminApi(HttpClient http)
{
    public string ImageUri(int id) => new Uri(http.BaseAddress ?? throw new InvalidOperationException("API base URL missing."), $"api/admin/public-catalog/images/{id}/content").ToString();
    public Task<List<AdminCategory>> Categories() => Read<List<AdminCategory>>("api/admin/public-catalog/categories");
    public Task<List<AdminSubcategory>> Subcategories() => Read<List<AdminSubcategory>>("api/admin/public-catalog/subcategories");
    public async Task<List<AdminProductSummary>> Products()
    {
        var page = await Read<AdminProductPage>("api/admin/public-catalog/products?page=1&pageSize=50");
        return page.Items;
    }
    public Task<AdminProduct> Product(int id) => Read<AdminProduct>($"api/admin/public-catalog/products/{id}");

    public Task<AdminCategory> SaveCategory(AdminCategory c) => c.Id == 0
        ? Send<AdminCategory>(HttpMethod.Post, "api/admin/public-catalog/categories", c.Write())
        : Send<AdminCategory>(HttpMethod.Put, $"api/admin/public-catalog/categories/{c.Id}", c.Write());
    public Task<AdminSubcategory> SaveSubcategory(AdminSubcategory c) => c.Id == 0
        ? Send<AdminSubcategory>(HttpMethod.Post, "api/admin/public-catalog/subcategories", c.Write())
        : Send<AdminSubcategory>(HttpMethod.Put, $"api/admin/public-catalog/subcategories/{c.Id}", c.Write());
    public Task<AdminProduct> CreateProduct(AdminProductWrite p) => Send<AdminProduct>(HttpMethod.Post,"api/admin/public-catalog/products", p);
    public Task<AdminProduct> UpdateProduct(int id,AdminProductWrite p) => Send<AdminProduct>(HttpMethod.Put,$"api/admin/public-catalog/products/{id}",p);
    public Task<ProductOperationResult> Publish(int id,string version,bool publish) =>
        Send<ProductOperationResult>(HttpMethod.Post,$"api/admin/public-catalog/products/{id}/{(publish ? "publish" : "unpublish")}",new { Version=version });
    public async Task<StagedImage> Stage(IBrowserFile file,string role)
    {
        if(file.Size is <=0 or >5242880)throw new InvalidOperationException("La imagen debe medir 5 MB como máximo.");
        using var multipart=new MultipartFormDataContent();
        using var stream=new StreamContent(file.OpenReadStream(5242880));
        stream.Headers.ContentType=new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
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
        if(response.IsSuccessStatusCode)return;
        var text=await response.Content.ReadAsStringAsync();
        var description=response.StatusCode switch
        {
            HttpStatusCode.Conflict => "Otro usuario modificó el contenido. Recargá antes de intentarlo de nuevo.",
            HttpStatusCode.BadRequest => "El servidor rechazó los datos o la protección CSRF.",
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "No tenés autorización para esta operación.",
            _ => $"La operación falló (HTTP {(int)response.StatusCode})."
        };
        // Do not echo raw server exceptions or untrusted HTML into the user interface.
        throw new InvalidOperationException(description);
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
public sealed class AdminProductSummary { public int Id {get;set;} public string Slug {get;set;}="";public bool IsPublished {get;set;} }
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
