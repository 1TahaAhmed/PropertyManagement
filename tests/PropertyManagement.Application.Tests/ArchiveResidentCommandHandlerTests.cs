using NSubstitute;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Residents;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Features.Residents.Archive;
using PropertyManagement.Domain.Entities.Residents;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Tests;

public sealed class ArchiveResidentCommandHandlerTests
{
    private readonly IResidentRepository _residentRepository = Substitute.For<IResidentRepository>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly ArchiveResidentCommandHandler _handler;
    private static readonly DateTime FixedNow = new(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc);

    public ArchiveResidentCommandHandlerTests()
    {
        _clock.UtcNow.Returns(FixedNow);
        _handler = new ArchiveResidentCommandHandler(_residentRepository, _clock);
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenResidentDoesNotExist()
    {
        var residentId = Guid.NewGuid();
        var orgId = Guid.NewGuid();

        _residentRepository.GetByIdAsync(residentId, orgId, Arg.Any<CancellationToken>())
            .Returns((Resident?)null);

        var command = new ArchiveResidentCommand(residentId, orgId);
        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Resident.NotFound", result.Errors.First().Code);
    }

    [Fact]
    public async Task Handle_ShouldArchiveResident_WhenValid()
    {
        var orgId = Guid.NewGuid();
        var resident = Resident.Create(orgId, "Kareem", "Mostafa", "+201012345678", FixedNow);

        _residentRepository.GetByIdAsync(resident.Id, orgId, Arg.Any<CancellationToken>())
            .Returns(resident);

        var command = new ArchiveResidentCommand(resident.Id, orgId);
        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(ResidentStatus.Archived, resident.Status);
        await _residentRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
