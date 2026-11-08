using NSubstitute;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Residents;
using PropertyManagement.Application.Features.Residents.Create;
using PropertyManagement.Domain.Entities.Residents;

namespace PropertyManagement.Application.Tests;

public sealed class CreateResidentCommandHandlerTests
{
    private readonly IResidentRepository _residentRepository = Substitute.For<IResidentRepository>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly CreateResidentCommandHandler _handler;
    private static readonly DateTime FixedNow = new(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc);

    public CreateResidentCommandHandlerTests()
    {
        _clock.UtcNow.Returns(FixedNow);
        _handler = new CreateResidentCommandHandler(_residentRepository, _clock);
    }

    [Fact]
    public async Task Handle_ShouldCreateResident_WhenCommandIsValid()
    {
        var orgId = Guid.NewGuid();
        var command = new CreateResidentCommand(
            OrganizationId: orgId,
            FirstName: "Kareem",
            LastName: "Mostafa",
            Phone: "+201012345678",
            Email: "kareem@test.com",
            PreferredName: "Kimo");

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Kareem", result.Value.FirstName);
        Assert.Equal("Mostafa", result.Value.LastName);
        Assert.Equal("kareem@test.com", result.Value.Email);
        Assert.Equal("Kimo Mostafa", result.Value.DisplayName);

        await _residentRepository.Received(1).AddAsync(Arg.Any<Resident>(), Arg.Any<CancellationToken>());
        await _residentRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
