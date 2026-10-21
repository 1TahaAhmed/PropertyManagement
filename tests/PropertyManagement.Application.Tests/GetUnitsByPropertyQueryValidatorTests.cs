using FluentValidation.TestHelper;
using PropertyManagement.Application.Features.Units.GetUnitsByProperty;

namespace PropertyManagement.Application.Tests;

public sealed class GetUnitsByPropertyQueryValidatorTests
{
    private readonly GetUnitsByPropertyQueryValidator _validator = new();

    [Fact]
    public void Validate_ShouldFail_WhenPropertyIdIsEmpty()
    {
        var query = new GetUnitsByPropertyQuery(Guid.Empty, null, null, null, null, 1, 10);
        var result = _validator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.PropertyId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_ShouldFail_WhenPageNumberIsInvalid(int invalidPage)
    {
        var query = new GetUnitsByPropertyQuery(Guid.NewGuid(), null, null, null, null, invalidPage, 10);
        var result = _validator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.PageNumber);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void Validate_ShouldFail_WhenPageSizeIsOutOfRange(int invalidSize)
    {
        var query = new GetUnitsByPropertyQuery(Guid.NewGuid(), null, null, null, null, 1, invalidSize);
        var result = _validator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.PageSize);
    }

    [Fact]
    public void Validate_ShouldPass_WhenQueryIsValid()
    {
        var query = new GetUnitsByPropertyQuery(Guid.NewGuid(), null, null, null, null, 1, 20);
        var result = _validator.TestValidate(query);
        result.ShouldNotHaveAnyValidationErrors();
    }
}