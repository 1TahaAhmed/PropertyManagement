using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropertyManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueConstraintToProperties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Units_PropertyId",
                table: "Units");

            migrationBuilder.DropIndex(
                name: "IX_Units_TenantId",
                table: "Units");

            migrationBuilder.DropIndex(
                name: "IX_Properties_TenantId_Name",
                table: "Properties");

            migrationBuilder.CreateIndex(
                name: "IX_Units_PropertyId",
                table: "Units",
                column: "PropertyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Units_TenantId",
                table: "Units",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Properties_TenantId_Name",
                table: "Properties",
                columns: new[] { "TenantId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Units_PropertyId",
                table: "Units");

            migrationBuilder.DropIndex(
                name: "IX_Units_TenantId",
                table: "Units");

            migrationBuilder.DropIndex(
                name: "IX_Properties_TenantId_Name",
                table: "Properties");

            migrationBuilder.CreateIndex(
                name: "IX_Units_PropertyId",
                table: "Units",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_Units_TenantId",
                table: "Units",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Properties_TenantId_Name",
                table: "Properties",
                columns: new[] { "TenantId", "Name" });
        }
    }
}
