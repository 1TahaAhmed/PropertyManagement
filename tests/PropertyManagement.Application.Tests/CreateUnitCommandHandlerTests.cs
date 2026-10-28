using NSubstitute;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Properties;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Features.Units.Create;
using PropertyManagement.Domain.Entities.Properties;
using PropertyManagement.Domain.Enums;
using Unit = PropertyManagement.Domain.Entities.Properties.Unit;

namespace PropertyManagement.Application.Tests;

public sealed class CreateUnitCommandHandlerTests
{
    private readonly IPropertyRepository _propertyRepository = Substitute.For<IPropertyRepository>();
    private readonly IUnitRepository _unitRepository = Substitute.For<IUnitRepository>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly CreateUnitCommandHandler _handler;

    private static readonly DateTime FixedNow = new(2026, 10, 21, 12, 0, 0, DateTimeKind.Utc);

    public CreateUnitCommandHandlerTests()
    {
        _clock.UtcNow.Returns(FixedNow);
        _handler = new CreateUnitCommandHandler(_propertyRepository, _unitRepository, _clock);
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenPropertyDoesNotExist()
    {
        var propertyId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        _propertyRepository.GetByIdAsync(propertyId, Arg.Any<CancellationToken>())
            .Returns((Property?)null);

        var command = new CreateUnitCommand(organizationId, propertyId, "101", UnitType.Apartment, 5000);
        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Property.NotFound", result.Errors.First().Code);
        Assert.Equal(ErrorType.NotFound, result.Errors.First().Type);
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenPropertyBelongsToDifferentOrganization()
    {
        var propertyId = Guid.NewGuid();
        var propertyOrganization = Guid.NewGuid();
        var attackingOrganization = Guid.NewGuid();

        var property = Property.Create(propertyOrganization, "Tower", "Cairo", PropertyType.Residential, FixedNow);
        _propertyRepository.GetByIdAsync(propertyId, Arg.Any<CancellationToken>())
            .Returns(property);

        var command = new CreateUnitCommand(attackingOrganization, propertyId, "101", UnitType.Apartment, 5000);
        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Property.NotFound", result.Errors.First().Code);
        Assert.Equal(ErrorType.NotFound, result.Errors.First().Type);
    }

    [Fact]
    public async Task Handle_ShouldReturnValidation_WhenPropertyIsArchived()
    {
        var organizationId = Guid.NewGuid();
        var property = Property.Create(organizationId, "Tower", "Cairo", PropertyType.Residential, FixedNow);
        property.Archive(FixedNow);

        _propertyRepository.GetByIdAsync(property.Id, Arg.Any<CancellationToken>())
            .Returns(property);

        var command = new CreateUnitCommand(organizationId, property.Id, "101", UnitType.Apartment, 5000);
        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Property.Archived", result.Errors.First().Code);
        Assert.Equal(ErrorType.Validation, result.Errors.First().Type);
    }

    [Fact]
    public async Task Handle_ShouldReturnConflict_WhenUnitNumberAlreadyExists()
    {
        var organizationId = Guid.NewGuid();
        var property = Property.Create(organizationId, "Tower", "Cairo", PropertyType.Residential, FixedNow);

        _propertyRepository.GetByIdAsync(property.Id, Arg.Any<CancellationToken>())
            .Returns(property);

        _unitRepository.ExistsByNumberAsync(organizationId, property.Id, "101", Arg.Any<CancellationToken>())
            .Returns(true);

        var command = new CreateUnitCommand(organizationId, property.Id, " 101 ", UnitType.Apartment, 5000);
        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Unit.DuplicateNumber", result.Errors.First().Code);
        Assert.Equal(ErrorType.Conflict, result.Errors.First().Type);
    }

    [Fact]
    public async Task Handle_ShouldSucceed_WhenInputsAreValid()
    {
        var organizationId = Guid.NewGuid();
        var property = Property.Create(organizationId, "Tower", "Cairo", PropertyType.Residential, FixedNow);

        _propertyRepository.GetByIdAsync(property.Id, Arg.Any<CancellationToken>())
            .Returns(property);

        _unitRepository.ExistsByNumberAsync(organizationId, property.Id, "101", Arg.Any<CancellationToken>())
            .Returns(false);

        var command = new CreateUnitCommand(organizationId, property.Id, "101", UnitType.Apartment, 5000, 1, 2, 1, 90, "Nice view");
        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("101", result.Value.UnitNumber);
        await _unitRepository.Received(1).AddAsync(Arg.Any<Unit>(), Arg.Any<CancellationToken>());
        await _unitRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
