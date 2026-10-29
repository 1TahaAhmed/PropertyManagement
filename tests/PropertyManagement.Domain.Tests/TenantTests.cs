using PropertyManagement.Domain.Entities.Tenants;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Domain.Tests;

public sealed class TenantTests
{
    private static readonly DateTime FixedNow = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid ValidOrgId = Guid.NewGuid();

    [Fact]
    public void Create_ShouldSucceed_WhenValidInputs()
    {
        var tenant = Tenant.Create(
            ValidOrgId,
            "Mohamed",
            "Ali",
            "mohamed.ali@example.com",
            "+201012345678",
            FixedNow,
            nationalIdOrPassport: "29001011234567",
            emergencyContactName: "Ahmed Ali",
            emergencyContactPhone: "+201098765432",
            notes: "VIP Client");

        Assert.NotEqual(Guid.Empty, tenant.Id);
        Assert.Equal(ValidOrgId, tenant.OrganizationId);
        Assert.Equal("Mohamed", tenant.FirstName);
        Assert.Equal("Ali", tenant.LastName);
        Assert.Equal("mohamed.ali@example.com", tenant.Email);
        Assert.Equal("+201012345678", tenant.PhoneNumber);
        Assert.Equal("29001011234567", tenant.NationalIdOrPassport);
        Assert.Equal("Ahmed Ali", tenant.EmergencyContactName);
        Assert.Equal("+201098765432", tenant.EmergencyContactPhone);
        Assert.Equal("VIP Client", tenant.Notes);
        Assert.Equal(TenantStatus.Active, tenant.Status);
        Assert.Equal(FixedNow, tenant.CreatedAt);
        Assert.Null(tenant.UpdatedAt);
        Assert.Equal("Mohamed Ali", tenant.FullName);
    }

    [Fact]
    public void Create_ShouldThrow_WhenOrganizationIdIsEmpty()
    {
        Assert.Throws<ArgumentException>(() =>
            Tenant.Create(
                Guid.Empty,
                "Mohamed",
                "Ali",
                "mohamed.ali@example.com",
                "+201012345678",
                FixedNow));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_ShouldThrow_WhenRequiredFieldIsInvalid(string? invalidValue)
    {
        // First name
        Assert.ThrowsAny<ArgumentException>(() =>
            Tenant.Create(ValidOrgId, invalidValue!, "Ali", "email@test.com", "123", FixedNow));

        // Last name
        Assert.ThrowsAny<ArgumentException>(() =>
            Tenant.Create(ValidOrgId, "Mohamed", invalidValue!, "email@test.com", "123", FixedNow));

        // Email
        Assert.ThrowsAny<ArgumentException>(() =>
            Tenant.Create(ValidOrgId, "Mohamed", "Ali", invalidValue!, "123", FixedNow));

        // Phone number
        Assert.ThrowsAny<ArgumentException>(() =>
            Tenant.Create(ValidOrgId, "Mohamed", "Ali", "email@test.com", invalidValue!, FixedNow));
    }

    [Fact]
    public void UpdateProfile_ShouldSucceed_AndSetUpdatedAt()
    {
        var tenant = Tenant.Create(ValidOrgId, "Mohamed", "Ali", "mohamed@test.com", "123", FixedNow);
        var updatedNow = FixedNow.AddDays(1);

        tenant.UpdateProfile(
            "Mohamed",
            "El-Sayed",
            "new.email@test.com",
            "999",
            "ID123",
            "Contact",
            "000",
            "New Notes",
            updatedNow);

        Assert.Equal("El-Sayed", tenant.LastName);
        Assert.Equal("new.email@test.com", tenant.Email);
        Assert.Equal("999", tenant.PhoneNumber);
        Assert.Equal("ID123", tenant.NationalIdOrPassport);
        Assert.Equal(updatedNow, tenant.UpdatedAt);
    }

    [Fact]
    public void UpdateProfile_ShouldThrow_WhenTenantIsBlacklisted()
    {
        var tenant = Tenant.Create(ValidOrgId, "Mohamed", "Ali", "mohamed@test.com", "123", FixedNow);
        tenant.Blacklist("Non-payment", FixedNow);

        Assert.Throws<InvalidOperationException>(() =>
            tenant.UpdateProfile("Mohamed", "Ali", "mohamed@test.com", "123", "", "", "", "", FixedNow.AddDays(1)));
    }

    [Fact]
    public void Blacklist_ShouldChangeStatus_AndAppendReason()
    {
        var tenant = Tenant.Create(ValidOrgId, "Mohamed", "Ali", "mohamed@test.com", "123", FixedNow, notes: "Initial note");
        tenant.Blacklist("Damaged property", FixedNow.AddDays(2));

        Assert.Equal(TenantStatus.Blacklisted, tenant.Status);
        Assert.Contains("[BLACKLISTED: Damaged property]", tenant.Notes);
        Assert.Equal(FixedNow.AddDays(2), tenant.UpdatedAt);
    }

    [Fact]
    public void DeactivateAndActivate_ShouldChangeStatusCorrectly()
    {
        var tenant = Tenant.Create(ValidOrgId, "Mohamed", "Ali", "mohamed@test.com", "123", FixedNow);

        tenant.Deactivate(FixedNow.AddHours(1));
        Assert.Equal(TenantStatus.Inactive, tenant.Status);
        Assert.Equal(FixedNow.AddHours(1), tenant.UpdatedAt);

        tenant.Activate(FixedNow.AddHours(2));
        Assert.Equal(TenantStatus.Active, tenant.Status);
        Assert.Equal(FixedNow.AddHours(2), tenant.UpdatedAt);
    }
}
