using PropertyManagement.Domain.Entities.Residents;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Domain.Tests;

public class ResidentTests
{
    private static readonly Guid ValidOrgId = Guid.NewGuid();
    private static readonly DateTime FixedNow = new(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_WithValidData_ShouldInitializeResidentCorrectly()
    {
        var resident = Resident.Create(
            ValidOrgId,
            firstName: "Omar",
            lastName: "Khaled",
            phone: "+201001234567",
            utcNow: FixedNow,
            middleName: "Hassan",
            preferredName: "Omy",
            email: "omar@example.com",
            alternatePhone: "+201007654321");

        Assert.NotEqual(Guid.Empty, resident.Id);
        Assert.Equal(ValidOrgId, resident.OrganizationId);
        Assert.Equal("Omar", resident.FirstName);
        Assert.Equal("Hassan", resident.MiddleName);
        Assert.Equal("Khaled", resident.LastName);
        Assert.Equal("Omy", resident.PreferredName);
        Assert.Equal("omar@example.com", resident.Email);
        Assert.Equal("+201001234567", resident.Phone);
        Assert.Equal("+201007654321", resident.AlternatePhone);
        Assert.Equal(ResidentStatus.Active, resident.Status);
        Assert.Equal("Omar Khaled", resident.FullName);
        Assert.Equal("Omy Khaled", resident.DisplayName);
        Assert.Equal(FixedNow, resident.CreatedAtUtc);
        Assert.Null(resident.UpdatedAtUtc);
        Assert.Null(resident.UserId);
        Assert.Empty(resident.EmergencyContacts);
    }

    [Fact]
    public void Create_WithoutPreferredName_DisplayNameShouldBeFullName()
    {
        var resident = Resident.Create(
            ValidOrgId,
            firstName: "Sarah",
            lastName: "Ahmed",
            phone: "+201112223334",
            utcNow: FixedNow);

        Assert.Equal("Sarah Ahmed", resident.DisplayName);
    }

    [Fact]
    public void Create_WithEmptyOrganizationId_ShouldThrowArgumentException()
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            Resident.Create(Guid.Empty, "Omar", "Khaled", "+201001234567", FixedNow));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithInvalidFirstName_ShouldThrowArgumentException(string? invalidFirst)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            Resident.Create(ValidOrgId, invalidFirst!, "Khaled", "+201001234567", FixedNow));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithInvalidLastName_ShouldThrowArgumentException(string? invalidLast)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            Resident.Create(ValidOrgId, "Omar", invalidLast!, "+201001234567", FixedNow));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithInvalidPhone_ShouldThrowArgumentException(string? invalidPhone)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            Resident.Create(ValidOrgId, "Omar", "Khaled", invalidPhone!, FixedNow));
    }

    [Fact]
    public void Create_WithInvalidEmailFormat_ShouldThrowArgumentException()
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            Resident.Create(ValidOrgId, "Omar", "Khaled", "+201001234567", FixedNow, email: "invalid-email"));
    }

    [Fact]
    public void UpdateDetails_WithValidData_ShouldUpdateResident()
    {
        var resident = Resident.Create(ValidOrgId, "Omar", "Khaled", "+201001234567", FixedNow);
        var updatedNow = FixedNow.AddDays(1);

        resident.UpdateDetails("Omar Updated", "Khaled New", "+201009999999", updatedNow, email: "new@example.com");

        Assert.Equal("Omar Updated", resident.FirstName);
        Assert.Equal("Khaled New", resident.LastName);
        Assert.Equal("+201009999999", resident.Phone);
        Assert.Equal("new@example.com", resident.Email);
        Assert.Equal(updatedNow, resident.UpdatedAtUtc);
    }

    [Fact]
    public void AddEmergencyContact_ShouldAddContactToCollection()
    {
        var resident = Resident.Create(ValidOrgId, "Omar", "Khaled", "+201001234567", FixedNow);

        var contact = resident.AddEmergencyContact(
            name: "Hassan Khaled",
            relationship: "Brother",
            phone: "+201005555555",
            utcNow: FixedNow,
            email: "brother@example.com",
            notes: "Available evenings");

        Assert.Single(resident.EmergencyContacts);
        Assert.Equal("Hassan Khaled", contact.Name);
        Assert.Equal("Brother", contact.Relationship);
        Assert.Equal(resident.Id, contact.ResidentId);
        Assert.Equal(resident.OrganizationId, contact.OrganizationId);
    }

    [Fact]
    public void UpdateEmergencyContact_ShouldModifyExistingContact()
    {
        var resident = Resident.Create(ValidOrgId, "Omar", "Khaled", "+201001234567", FixedNow);
        var contact = resident.AddEmergencyContact("Hassan", "Brother", "+201005555555", FixedNow);
        var updateTime = FixedNow.AddHours(2);

        resident.UpdateEmergencyContact(contact.Id, "Hassan K.", "Father", "+201008888888", updateTime, notes: "Changed number");

        Assert.Equal("Hassan K.", contact.Name);
        Assert.Equal("Father", contact.Relationship);
        Assert.Equal("+201008888888", contact.Phone);
        Assert.Equal("Changed number", contact.Notes);
    }

    [Fact]
    public void RemoveEmergencyContact_ShouldRemoveFromCollection()
    {
        var resident = Resident.Create(ValidOrgId, "Omar", "Khaled", "+201001234567", FixedNow);
        var contact = resident.AddEmergencyContact("Hassan", "Brother", "+201005555555", FixedNow);

        resident.RemoveEmergencyContact(contact.Id, FixedNow.AddHours(1));

        Assert.Empty(resident.EmergencyContacts);
    }

    [Fact]
    public void Archive_ShouldChangeStatusToArchived()
    {
        var resident = Resident.Create(ValidOrgId, "Omar", "Khaled", "+201001234567", FixedNow);
        var archiveTime = FixedNow.AddMonths(6);

        resident.Archive(archiveTime);

        Assert.Equal(ResidentStatus.Archived, resident.Status);
        Assert.Equal(archiveTime, resident.UpdatedAtUtc);
    }

    [Fact]
    public void MutatingArchivedResident_ShouldThrowInvalidOperationException()
    {
        var resident = Resident.Create(ValidOrgId, "Omar", "Khaled", "+201001234567", FixedNow);
        resident.Archive(FixedNow);

        Assert.Throws<InvalidOperationException>(() =>
            resident.UpdateDetails("New", "Name", "+201000000000", FixedNow));

        Assert.Throws<InvalidOperationException>(() =>
            resident.AddEmergencyContact("Contact", "Friend", "+201000000000", FixedNow));

        Assert.Throws<InvalidOperationException>(() =>
            resident.Deactivate(FixedNow));

        Assert.Throws<InvalidOperationException>(() =>
            resident.Activate(FixedNow));

        Assert.Throws<InvalidOperationException>(() =>
            resident.LinkUser(Guid.NewGuid(), FixedNow));
    }

    [Fact]
    public void LinkAndUnlinkUser_ShouldUpdateUserIdCorrectly()
    {
        var resident = Resident.Create(ValidOrgId, "Omar", "Khaled", "+201001234567", FixedNow);
        var userId = Guid.NewGuid();

        resident.LinkUser(userId, FixedNow);
        Assert.Equal(userId, resident.UserId);

        resident.UnlinkUser(FixedNow.AddDays(1));
        Assert.Null(resident.UserId);
    }
}
