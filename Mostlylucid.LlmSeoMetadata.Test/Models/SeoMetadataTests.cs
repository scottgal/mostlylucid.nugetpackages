using Mostlylucid.LlmSeoMetadata.Models;

namespace Mostlylucid.LlmSeoMetadata.Test.Models;

public class SeoMetadataTests
{
    [Fact]
    public void SeoMetadata_DefaultGeneratedAt_IsRecentUtcTime()
    {
        var metadata = new SeoMetadata();

        var timeDiff = DateTime.UtcNow - metadata.GeneratedAt;

        Assert.True(timeDiff.TotalSeconds < 5);
    }

    [Fact]
    public void OpenGraphMetadata_DefaultType_IsWebsite()
    {
        var og = new OpenGraphMetadata();

        Assert.Equal("website", og.Type);
    }

    [Fact]
    public void JsonLdMetadata_DefaultContext_IsSchemaOrg()
    {
        var jsonLd = new JsonLdMetadata();

        Assert.Equal("https://schema.org", jsonLd.Context);
    }

    [Fact]
    public void JsonLdMetadata_DefaultType_IsArticle()
    {
        var jsonLd = new JsonLdMetadata();

        Assert.Equal("Article", jsonLd.Type);
    }

    [Fact]
    public void JsonLdAuthor_DefaultType_IsPerson()
    {
        var author = new JsonLdAuthor();

        Assert.Equal("Person", author.Type);
    }

    [Fact]
    public void JsonLdOrganization_DefaultType_IsOrganization()
    {
        var org = new JsonLdOrganization();

        Assert.Equal("Organization", org.Type);
    }

    [Fact]
    public void JsonLdOffer_DefaultType_IsOffer()
    {
        var offer = new JsonLdOffer();

        Assert.Equal("Offer", offer.Type);
    }

    [Fact]
    public void JsonLdBrand_DefaultType_IsBrand()
    {
        var brand = new JsonLdBrand();

        Assert.Equal("Brand", brand.Type);
    }

    [Fact]
    public void JsonLdAggregateRating_DefaultType_IsAggregateRating()
    {
        var rating = new JsonLdAggregateRating();

        Assert.Equal("AggregateRating", rating.Type);
    }

    [Fact]
    public void GenerationRequest_DefaultValues_AreAllTrue()
    {
        var request = new GenerationRequest
        {
            Content = new ContentInput { Title = "Test", Content = "Test" }
        };

        Assert.True(request.GenerateMetaDescription);
        Assert.True(request.GenerateOpenGraph);
        Assert.True(request.GenerateJsonLd);
        Assert.True(request.GenerateKeywords);
        Assert.True(request.UseCache);
        Assert.False(request.ForceRegenerate);
    }

    [Fact]
    public void GenerationResponse_DefaultFromCache_IsFalse()
    {
        var response = new GenerationResponse();

        Assert.False(response.FromCache);
        Assert.False(response.Success);
    }
}
