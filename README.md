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
  - JWT signing secrets are supplied through user secrets or environment variables, never committed to source control.

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

2. **Configure the JWT signing secret**:

   The development configuration intentionally contains no default signing secret. Generate a unique random value of at least 32 characters and store it in .NET User Secrets:

   ```bash
   dotnet user-secrets set "Jwt:Secret" "<your-random-secret-at-least-32-characters>" --project src/PropertyManagement.Api
   ```

   The API validates the secret at startup. Do not put production or shared secrets in `appsettings*.json` or commit them.

3. **Configure the database** (if needed):

   Set `ConnectionStrings:MyConn` using User Secrets rather than committing credentials:

   ```bash
   dotnet user-secrets set "ConnectionStrings:MyConn" "<your-sql-server-connection-string>" --project src/PropertyManagement.Api
   ```

4. **Run the Database Migrations**:

   ```bash
   dotnet ef database update --project src/PropertyManagement.Infrastructure --startup-project src/PropertyManagement.Api
   ```

5. **Run the API**:

   ```bash
   dotnet run --project src/PropertyManagement.Api
   ```

   Navigate to `https://localhost:7261/swagger` to explore the interactive API documentation.

## Authorizing requests in Swagger

1. Execute `POST /api/v1/Auth/login` and copy the `accessToken` value from the response.
2. Click **Authorize** at the top of Swagger UI.
3. Paste the access token only. Do not type the `Bearer ` prefix; the HTTP Bearer scheme adds it automatically.
4. Close the dialog and execute a protected endpoint such as `POST /api/v1/Properties`.

The generated curl command for a protected endpoint must contain an `Authorization: Bearer <token>` header. Login, registration, refresh-token, and other public authentication endpoints do not require this header. Obtain a fresh access token after it expires.

## Running with Docker Compose

Copy `.env.example` to `.env`, then set a strong, unique `MSSQL_SA_PASSWORD` and a random `JWT_SECRET` of at least 32 characters before starting the stack:

```bash
docker compose up --build
```

The compose file intentionally refuses to start when either secret is missing.

## Running Tests

Run the full test suite (Domain, Application, Integration):

```bash
dotnet test PropertyManagement.slnx
```
