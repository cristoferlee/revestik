using Revestik.Api.Endpoints.PublicCatalog;

namespace Revestik.Api.Tests.PublicProjects;

public sealed class PublicProjectTagValidationTests
{
    private static PublicProjectTagEndpoints.TagWrite Valid() =>
        new(100, .33m, .20m, .54m, .26m, 0, "Piso 60x60", null, null, null);

    [Fact]
    public void ValidPositionsAndShortNamesAreAccepted()
    {
        Assert.True(PublicProjectTagEndpoints.ValidTagWrite(Valid()));
        Assert.True(PublicProjectTagEndpoints.ValidTagWrite(Valid() with
        {
            AnchorX = 0m, AnchorY = 1m, LabelX = 1m, LabelY = 0m
        }));
    }

    [Fact]
    public void InvalidCoordinatesOrPrecisionAreRejected()
    {
        Assert.False(PublicProjectTagEndpoints.ValidTagWrite(Valid() with { AnchorX = -0.01m }));
        Assert.False(PublicProjectTagEndpoints.ValidTagWrite(Valid() with { LabelX = 1.01m }));
        Assert.False(PublicProjectTagEndpoints.ValidTagWrite(Valid() with { AnchorY = .12345m }));
        Assert.False(PublicProjectTagEndpoints.ValidTagWrite(Valid() with { DisplayOrder = -1 }));
    }

    [Fact]
    public void ExcessiveCustomLabelsAreRejected()
    {
        Assert.False(PublicProjectTagEndpoints.ValidTagWrite(Valid() with
        {
            CustomLabelEs = new string('x', 121)
        }));
        Assert.False(PublicProjectTagEndpoints.ValidTagWrite(Valid() with
        {
            CustomLabelDe = new string('x', 121)
        }));
    }
}
