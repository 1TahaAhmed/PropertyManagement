using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropertyManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailConfirmationResendAttempts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmailConfirmationResendAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailConfirmationResendAttempts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmailConfirmationResendAttempts_IpAddress_RequestedAt",
                table: "EmailConfirmationResendAttempts",
                columns: new[] { "IpAddress", "RequestedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailConfirmationResendAttempts_UserId_RequestedAt",
                table: "EmailConfirmationResendAttempts",
                columns: new[] { "UserId", "RequestedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmailConfirmationResendAttempts");
        }
    }
}
