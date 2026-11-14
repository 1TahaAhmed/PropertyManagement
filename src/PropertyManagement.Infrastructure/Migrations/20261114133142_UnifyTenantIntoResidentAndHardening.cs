using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropertyManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UnifyTenantIntoResidentAndHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NationalIdOrPassport",
                table: "Residents",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "Residents",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "RefreshTokens",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.CreateIndex(
                name: "IX_Residents_OrganizationId_Status",
                table: "Residents",
                columns: new[] { "OrganizationId", "Status" });

            if (ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer")
            {
                migrationBuilder.Sql("""
                    INSERT INTO [Residents] (
                        [Id],
                        [OrganizationId],
                        [FirstName],
                        [MiddleName],
                        [LastName],
                        [PreferredName],
                        [Email],
                        [Phone],
                        [AlternatePhone],
                        [NationalIdOrPassport],
                        [Notes],
                        [Status],
                        [UserId],
                        [CreatedAtUtc],
                        [UpdatedAtUtc]
                    )
                    SELECT
                        t.[Id],
                        t.[OrganizationId],
                        t.[FirstName],
                        NULL,
                        t.[LastName],
                        NULL,
                        NULLIF(t.[Email], N''),
                        t.[PhoneNumber],
                        NULL,
                        ISNULL(t.[NationalIdOrPassport], N''),
                        ISNULL(t.[Notes], N''),
                        CASE t.[Status]
                            WHEN 1 THEN 1
                            WHEN 2 THEN 2
                            WHEN 3 THEN 4
                            ELSE 1
                        END,
                        NULL,
                        t.[CreatedAt],
                        t.[UpdatedAt]
                    FROM [Tenants] t
                    WHERE NOT EXISTS (
                        SELECT 1 FROM [Residents] r WHERE r.[Id] = t.[Id]
                    );

                    INSERT INTO [EmergencyContacts] (
                        [Id],
                        [ResidentId],
                        [OrganizationId],
                        [Name],
                        [Relationship],
                        [Phone],
                        [Email],
                        [Notes],
                        [CreatedAtUtc],
                        [UpdatedAtUtc]
                    )
                    SELECT
                        NEWID(),
                        t.[Id],
                        t.[OrganizationId],
                        t.[EmergencyContactName],
                        N'Emergency Contact',
                        t.[EmergencyContactPhone],
                        NULL,
                        N'Migrated from legacy Tenants table',
                        t.[CreatedAt],
                        t.[UpdatedAt]
                    FROM [Tenants] t
                    WHERE LEN(LTRIM(RTRIM(ISNULL(t.[EmergencyContactName], N'')))) > 0
                      AND LEN(LTRIM(RTRIM(ISNULL(t.[EmergencyContactPhone], N'')))) > 0
                      AND EXISTS (SELECT 1 FROM [Residents] r WHERE r.[Id] = t.[Id]);
                    """);
            }

            migrationBuilder.DropTable(
                name: "Tenants");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Tenants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    EmergencyContactName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    EmergencyContactPhone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NationalIdOrPassport = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tenants_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_OrganizationId",
                table: "Tenants",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_OrganizationId_Email",
                table: "Tenants",
                columns: new[] { "OrganizationId", "Email" });

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_OrganizationId_PhoneNumber",
                table: "Tenants",
                columns: new[] { "OrganizationId", "PhoneNumber" });

            migrationBuilder.DropIndex(
                name: "IX_Residents_OrganizationId_Status",
                table: "Residents");

            migrationBuilder.DropColumn(
                name: "NationalIdOrPassport",
                table: "Residents");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "Residents");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "RefreshTokens");
        }
    }
}
