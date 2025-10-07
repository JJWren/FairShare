using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FairShare.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ParentProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    IsArchived = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "BLOB", rowVersion: true, nullable: true),
                    HasPrimaryCustody = table.Column<bool>(type: "INTEGER", nullable: false),
                    MonthlyGrossIncome = table.Column<int>(type: "INTEGER", nullable: false),
                    PreexistingChildSupport = table.Column<int>(type: "INTEGER", nullable: false),
                    PreexistingAlimony = table.Column<int>(type: "INTEGER", nullable: false),
                    WorkRelatedChildcareCosts = table.Column<int>(type: "INTEGER", nullable: false),
                    HealthcareCoverageCosts = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParentProfiles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ParentProfiles_DisplayName",
                table: "ParentProfiles",
                column: "DisplayName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ParentProfiles");
        }
    }
}
