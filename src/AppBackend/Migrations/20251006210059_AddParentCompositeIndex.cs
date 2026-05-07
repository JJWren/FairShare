using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FairShare.AppBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddParentCompositeIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ParentProfile_DuplicateSig",
                table: "ParentProfiles",
                columns: new[] { "MonthlyGrossIncome", "PreexistingChildSupport", "PreexistingAlimony", "WorkRelatedChildcareCosts", "HealthcareCoverageCosts", "HasPrimaryCustody" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ParentProfile_DuplicateSig",
                table: "ParentProfiles");
        }
    }
}






