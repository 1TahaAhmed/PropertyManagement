using FluentValidation.TestHelper;
using PropertyManagement.Application.Features.Properties.GetProperties;

namespace PropertyManagement.Application.Tests;

public sealed class GetPropertiesQueryValidatorTests
{
    private readonly GetPropertiesQueryValidator _validator = new();

    [Fact]
    public void Validate_ShouldFail_WhenTenantIdIsEmpty()
    {
        var query = new GetPropertiesQuery(Guid.Empty, null, null, null, 1, 10);
        var result = _validator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.TenantId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_ShouldFail_WhenPageNumberIsZeroOrNegative(int invalidPage)
    {
        var query = new GetPropertiesQuery(Guid.NewGuid(), null, null, null, invalidPage, 10);
        var result = _validator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.PageNumber);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(101)]
    [InlineData(500)]
    public void Validate_ShouldFail_WhenPageSizeIsOutOfRange(int invalidSize)
    {
        var query = new GetPropertiesQuery(Guid.NewGuid(), null, null, null, 1, invalidSize);
        var result = _validator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.PageSize);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, 10)]
    [InlineData(5, 50)]
    [InlineData(10, 100)]
    public void Validate_ShouldPass_WhenPaginationIsValid(int pageNumber, int pageSize)
    {
        var query = new GetPropertiesQuery(Guid.NewGuid(), "Tower", null, null, pageNumber, pageSize);
        var result = _validator.TestValidate(query);
        result.ShouldNotHaveAnyValidationErrors();
    }
}