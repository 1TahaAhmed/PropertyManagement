using Microsoft.AspNetCore.Identity;

namespace PropertyManagement.Infrastructure.Identity
{
    public class ApplicationUser : IdentityUser
    {
        public string FirstName { get; private set; } = string.Empty;
        public string LastName { get; private set; } = string.Empty;
        public string Address { get; private set; } = string.Empty;
        public DateTime CreatedAt { get; private set; } 
    }
}
