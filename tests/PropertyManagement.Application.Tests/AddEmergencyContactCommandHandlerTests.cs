using NSubstitute;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Residents;
using PropertyManagement.Application.Features.Residents.EmergencyContacts;
using PropertyManagement.Domain.Entities.Residents;

namespace PropertyManagement.Application.Tests;

public sealed class AddEmergencyContactCommandHandlerTests
{
    private readonly IResidentRepository _residentRepository = Substitute.For<IResidentRepository>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly AddEmergencyContactCommandHandler _handler;
    private static readonly DateTime FixedNow = new(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc);

    public AddEmergencyContactCommandHandlerTests()
    {
        _clock.UtcNow.Returns(FixedNow);
        _handler = new AddEmergencyContactCommandHandler(_residentRepository, _clock);
    }

    [Fact]
    public async Task Handle_ShouldAddEmergencyContact_WhenResidentExists()
    {
        var orgId = Guid.NewGuid();
        var resident = Resident.Create(orgId, "Kareem", "Mostafa", "+201012345678", FixedNow);

        _residentRepository.GetByIdWithContactsAsync(resident.Id, orgId, Arg.Any<CancellationToken>())
            .Returns(resident);

        var command = new AddEmergencyContactCommand(
            ResidentId: resident.Id,
            OrganizationId: orgId,
            Name: "Youssef Mostafa",
            Relationship: "Brother",
            Phone: "+201055555555");

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Youssef Mostafa", result.Value.Name);
        Assert.Equal("Brother", result.Value.Relationship);
        Assert.Single(resident.EmergencyContacts);
        await _residentRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
