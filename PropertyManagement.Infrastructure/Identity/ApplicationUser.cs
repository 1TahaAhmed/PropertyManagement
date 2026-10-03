using Microsoft.AspNetCore.Identity;

namespace PropertyManagement.Infrastructure.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    protected ApplicationUser()
    {
    }

    private ApplicationUser(
        string firstName,
        string lastName,
        string address,
        string email)
    {
        FirstName = firstName;
        LastName = lastName;
        Address = address;
        Email = email;
        UserName = email;
        CreatedAt = DateTime.UtcNow;
    }

    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string Address { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }

    public static ApplicationUser Create(
        string firstName,
        string lastName,
        string address,
        string email)
    {
        ArgumentException.ThrowIfNullOrEmpty(firstName);
        ArgumentException.ThrowIfNullOrEmpty(lastName);
        ArgumentException.ThrowIfNullOrEmpty(email);

        return new ApplicationUser(
            firstName.Trim(),
            lastName.Trim(),
            address?.Trim() ?? string.Empty,
            email.Trim().ToLowerInvariant());
    }

    public void UpdateProfile(string firstName, string lastName, string address)
    {
        ArgumentException.ThrowIfNullOrEmpty(firstName);
        ArgumentException.ThrowIfNullOrEmpty(lastName);

        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        Address = address?.Trim() ?? string.Empty;
    }
}