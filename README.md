# PropertyManagement (.NET 10 Clean Architecture)

Enterprise-grade Multi-Tenant Real Estate & Property Management platform built with .NET 10, EF Core, CQRS (MediatR), ASP.NET Core Identity, and Clean Architecture.

## Architecture & Design Principles

- **Clean Architecture & Separation of Concerns**:
  - `PropertyManagement.Domain`: Entities, Enums, Domain Rules, Invariants.
  - `PropertyManagement.Application`: CQRS Commands, Queries, Handlers, FluentValidation, Security abstractions.
  - `PropertyManagement.Infrastructure`: EF Core, SQL Server persistence, SmtpEmailSender, JWT Token generation, Multi-Tenant queries.
  - `PropertyManagement.Api`: REST API Controllers, RFC 7807 ProblemDetails, Swagger Documentation, JWT Authentication, Permissions policies.
- **Multi-Tenancy Isolation**:
  - Global tenant filters partition data securely by `OrganizationId`.
  - Roles & Permissions: `Owner`, `Admin`, `PropertyManager`, `Staff`.
- **JWT & Refresh Token Rotation**:
  - 15-minute access token lifetime with 5-minute clock-skew tolerance.
  - Cryptographic 64-byte refresh tokens stored exclusively as SHA-256 hashes in database.

## Prerequisites

- .NET 10.0 SDK
- SQL Server 2022+ (LocalDB, Express, or Azure SQL)
- (Optional) Docker & Docker Compose

## Quick Start

1. **Clone the repository**:
   ```bash
   git clone https://github.com/1TahaAhmed/PropertyManagement.git
   cd PropertyManagement
   ```

2. **Configure Connection String & JWT**:
   Set `ConnectionStrings:MyConn` and `Jwt:Secret` (minimum 32 characters) in `appsettings.Development.json` or user secrets.

3. **Run the Database Migrations**:
   ```bash
   dotnet ef database update --project src/PropertyManagement.Infrastructure --startup-project src/PropertyManagement.Api
   ```

4. **Run the API**:
   ```bash
   dotnet run --project src/PropertyManagement.Api
   ```
   Navigate to `https://localhost:7261/swagger` to explore the interactive API documentation.

## Running Tests

Run the full test suite (Domain, Application, Integration):
```bash
dotnet test PropertyManagement.slnx
```
