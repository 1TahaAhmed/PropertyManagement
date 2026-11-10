using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PropertyManagement.Infrastructure.Persistence;

public sealed class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();

        // Design-time connection string for generating migrations
        optionsBuilder.UseSqlServer(
            "Server=(localdb)\\mssqllocaldb;Database=PropertyManagementDb_DesignTime;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True");

        return new ApplicationDbContext(optionsBuilder.Options, null);
    }
}
