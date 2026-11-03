using Revestik.Api.Services.PublicCatalog;
using Xunit;

namespace Revestik.Api.Tests.PublicCatalog;

public sealed class PublicCatalogMediaValidationTests
{
    [Fact]
    public void RejectsEmptyImage()
    {
        Assert.Throws<InvalidDataException>(() => PublicCatalogMediaValidation.Validate([],"image.png","ProductCover"));
    }

    [Fact]
    public void RejectsOversizedFile()
    {
        Assert.Throws<InvalidDataException>(()=>PublicCatalogMediaValidation.Validate(new byte[5*1024*1024+1],"image.png","ProductCover"));
    }

    [Theory]
    [InlineData("ProductCover")]
    [InlineData("ProductGallery")]
    [InlineData("CategoryGallery")]
    [InlineData("CategoryHeroFallback")]
    public void RejectsGifOutsideMainHero(string role)
    {
        var gif=System.Text.Encoding.ASCII.GetBytes("GIF89aThisIsNotAnActualGif");
        Assert.Throws<InvalidDataException>(()=>PublicCatalogMediaValidation.Validate(gif,"image.gif",role));
    }

    [Fact]
    public void AcceptsWellFormedTwoFrameHeroGif()
    {
        var bytes=Convert.FromBase64String("R0lGODlhAgACAIEAAP8AAAAAAAAAAAAAACH/C05FVFNDQVBFMi4wAwEAAAAh+QQAGQAAACwAAAAAAgACAAAIBgABCAQQEAAh+QQBGQABACwAAAAAAgACAIEAAP8AAAAAAAAAAAAIBgABCAQQEAA7");
        var result=PublicCatalogMediaValidation.Validate(bytes,"hero.gif","CategoryHeroPrimary");
        Assert.Equal("image/gif",result.ContentType);
        Assert.Equal(2,result.FrameCount);
        Assert.Equal(2,result.Width);
        Assert.Equal(2,result.Height);
    }

    [Fact]
    public void RejectsForgedImageExtension()
    {
        Assert.Throws<InvalidDataException>(()=>PublicCatalogMediaValidation.Validate(System.Text.Encoding.ASCII.GetBytes("hello"),"image.webp","ProductCover"));
    }
}
