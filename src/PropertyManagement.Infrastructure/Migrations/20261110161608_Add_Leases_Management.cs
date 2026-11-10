using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropertyManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Add_Leases_Management : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Leases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RentAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SecurityDeposit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Frequency = table.Column<int>(type: "int", nullable: false),
                    DueDayOfMonth = table.Column<int>(type: "int", nullable: false),
                    GracePeriodDays = table.Column<int>(type: "int", nullable: false),
                    LateFeePercentage = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    ParentLeaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TerminationDate = table.Column<DateOnly>(type: "date", nullable: true),
                    TerminationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TerminationNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    TerminatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TerminatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Leases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Leases_Leases_ParentLeaseId",
                        column: x => x.ParentLeaseId,
                        principalTable: "Leases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Leases_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Leases_Units_UnitId",
                        column: x => x.UnitId,
                        principalTable: "Units",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LeaseResidents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResidentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<int>(type: "int", nullable: false),
                    AddedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaseResidents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeaseResidents_Leases_LeaseId",
                        column: x => x.LeaseId,
                        principalTable: "Leases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LeaseResidents_Residents_ResidentId",
                        column: x => x.ResidentId,
                        principalTable: "Residents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LeaseResidents_LeaseId_ResidentId",
                table: "LeaseResidents",
                columns: new[] { "LeaseId", "ResidentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeaseResidents_OrganizationId_ResidentId",
                table: "LeaseResidents",
                columns: new[] { "OrganizationId", "ResidentId" });

            migrationBuilder.CreateIndex(
                name: "IX_LeaseResidents_ResidentId",
                table: "LeaseResidents",
                column: "ResidentId");

            migrationBuilder.CreateIndex(
                name: "IX_Leases_OrganizationId_PropertyId_Status",
                table: "Leases",
                columns: new[] { "OrganizationId", "PropertyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Leases_OrganizationId_UnitId_StartDate_EndDate",
                table: "Leases",
                columns: new[] { "OrganizationId", "UnitId", "StartDate", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Leases_OrganizationId_UnitId_Status",
                table: "Leases",
                columns: new[] { "OrganizationId", "UnitId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Leases_ParentLeaseId",
                table: "Leases",
                column: "ParentLeaseId");

            migrationBuilder.CreateIndex(
                name: "IX_Leases_PropertyId",
                table: "Leases",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_Leases_UnitId",
                table: "Leases",
                column: "UnitId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LeaseResidents");

            migrationBuilder.DropTable(
                name: "Leases");
        }
    }
}
