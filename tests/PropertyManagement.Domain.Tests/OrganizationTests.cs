using PropertyManagement.Domain.Entities.Organizations;
using PropertyManagement.Domain.Enums;
using Xunit;

namespace PropertyManagement.Domain.Tests;

public sealed class OrganizationTests
{
    private static readonly DateTime FixedNow = new(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void GenerateSlug_WithEnglishName_ShouldProduceCleanSlug()
    {
        var slug = Organization.GenerateSlug("Al Nour Real Estate");
        Assert.Equal("al-nour-real-estate", slug);
    }

    [Fact]
    public void GenerateSlug_WithArabicName_ShouldFallbackToValidOrgSlug()
    {
        var slug = Organization.GenerateSlug("شركة النيل للعقارات");

        Assert.StartsWith("org-", slug);
        Assert.True(slug.Length >= 6);
    }

    [Fact]
    public void GenerateSlug_WithSpecialCharacters_ShouldStripThem()
    {
        var slug = Organization.GenerateSlug("Al-Nour & Sons @ Co. #1!");
        Assert.Equal("al-nour-sons-co-1", slug);
    }

    [Fact]
    public void GenerateSlug_WithMultipleSpacesAndHyphens_ShouldCollapseHyphens()
    {
        var slug = Organization.GenerateSlug("   A    B --- C   ");
        Assert.Equal("a-b-c", slug);
    }

    [Fact]
    public void GenerateSlug_WithLeadingAndTrailingHyphens_ShouldTrimThem()
    {
        var slug = Organization.GenerateSlug("---test-company---");
        Assert.Equal("test-company", slug);
    }

    [Fact]
    public void Create_ShouldSucceed_WhenValidInputs()
    {
        var org = Organization.Create("Delta Real Estate", FixedNow);
        
        Assert.NotEqual(Guid.Empty, org.Id);
        Assert.Equal("Delta Real Estate", org.Name);
        Assert.Equal("delta-real-estate", org.Identifier);
        Assert.Equal(OrganizationStatus.Active, org.Status);
        Assert.Equal(FixedNow, org.CreatedAt);
    }

    [Fact]
    public void UpdateName_ShouldUpdateNameWithoutChangingIdentifier()
    {
        var org = Organization.Create("Original Name", FixedNow);
        var originalIdentifier = org.Identifier;
        var updateTime = FixedNow.AddDays(1);

        org.UpdateName("Brand New Name", updateTime);

        Assert.Equal("Brand New Name", org.Name);
        Assert.Equal(originalIdentifier, org.Identifier);
        Assert.Equal(updateTime, org.UpdatedAt);
    }
}