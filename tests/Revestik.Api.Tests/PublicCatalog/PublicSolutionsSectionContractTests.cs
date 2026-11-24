using Revestik.Shared.PublicSite;

namespace Revestik.Api.Tests.PublicCatalog;

public sealed class PublicSolutionsSectionContractTests
{
    [Fact]
    public void SevenSectionsHaveApprovedCopyInAllThreeLanguages()
    {
        Assert.Equal(7, SolutionsSectionDefaults.Keys.Length);
        foreach (var key in SolutionsSectionDefaults.Keys)
        {
            var copy = SolutionsSectionDefaults.Create(key);
            Assert.Equal(key, copy.Key);
            Assert.True(SolutionsSectionDefaults.ValidCopy(key, copy.Es, required: true));
            Assert.True(SolutionsSectionDefaults.ValidCopy(key, copy.En, required: true));
            Assert.True(SolutionsSectionDefaults.ValidCopy(key, copy.De, required: true));
        }
    }

    [Fact]
    public void BoundedKeysAndFieldsPreventCreationAndReordering()
    {
        Assert.False(SolutionsSectionDefaults.IsKnown("solutions")); // Reserved for Home.
        Assert.False(SolutionsSectionDefaults.IsKnown("navigation"));
        Assert.False(SolutionsSectionDefaults.AllowsField("interior-flooring", "eyebrow"));
        Assert.False(SolutionsSectionDefaults.AllowsField("cta", "image"));
        Assert.True(SolutionsSectionDefaults.AllowsField("walls-facades", "description"));
    }

    [Fact]
    public void OneLanguageCanBeChangedWithoutMutatingOtherTranslations()
    {
        var copy = SolutionsSectionDefaults.Create("hero");
        var savedEnglish = copy.En!.Title;
        var savedGerman = copy.De!.Description;
        var editing = copy.Clone();
        editing.Es.Title = "Nuevo título";
        Assert.Equal(savedEnglish, editing.En!.Title);
        Assert.Equal(savedGerman, editing.De!.Description);
        Assert.Equal("Nuevo título", editing.Value("es", "title"));
    }

    [Fact]
    public void ExplicitMissingTranslationFallsBackToSpanish()
    {
        var copy = SolutionsSectionDefaults.Create("hero");
        copy.De = null;
        Assert.Equal(copy.Es.Title, copy.Value("de", "title"));
        Assert.Equal(copy.En!.Title, copy.Value("en", "title"));
    }

    [Fact]
    public void OptionalPartialTranslationUsesFieldLevelSpanishFallback()
    {
        var section = SolutionsSectionDefaults.Create("hero");
        section.En = new SolutionsSectionCopy { Title = "An edited English heading" };
        Assert.True(SolutionsSectionDefaults.ValidCopy("hero", section.En, required: false));
        Assert.Equal("An edited English heading", section.Value("en", "title"));
        Assert.Equal(section.Es.Description, section.Value("en", "description"));
    }

    [Fact]
    public void ApplicationsRejectDescription()
    {
        var copy = SolutionsSectionDefaults.Copy("applications", "es");
        copy.Description = "No corresponde aquí";
        Assert.False(SolutionsSectionDefaults.ValidCopy("applications", copy, required: true));
        copy.Description = "";
        Assert.True(SolutionsSectionDefaults.ValidCopy("applications", copy, required: true));
    }
}
