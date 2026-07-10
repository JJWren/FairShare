using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FairShare.Api.Migrations
{
    /// <summary>
    /// Enforces the "display name is the natural key within one user's saved parents" rule
    /// at the database level, so concurrent saves can't create same-named duplicates that
    /// the application-level upsert check would miss.
    /// </summary>
    public partial class ParentNameOwnerUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing databases may already hold same-named duplicates created before the
            // upsert-by-name behavior; keep the most recently touched active record per
            // (owner, name) and archive the rest so the unique index can be created.
            // Unowned (NULL owner) legacy rows are exempt, matching the index semantics
            // below (SQLite treats NULLs as distinct in unique indexes).
            migrationBuilder.Sql(
                """
                UPDATE "ParentProfiles"
                SET "IsArchived" = 1, "UpdatedUtc" = CURRENT_TIMESTAMP
                WHERE "IsArchived" = 0
                  AND "OwnerUserId" IS NOT NULL
                  AND "Id" NOT IN (
                      SELECT "Id" FROM (
                          SELECT "Id",
                                 ROW_NUMBER() OVER (
                                     PARTITION BY "OwnerUserId", lower("DisplayName")
                                     ORDER BY COALESCE("UpdatedUtc", "CreatedUtc") DESC
                                 ) AS rn
                          FROM "ParentProfiles"
                          WHERE "IsArchived" = 0 AND "OwnerUserId" IS NOT NULL
                      )
                      WHERE rn = 1
                  );
                """);

            // Partial unique index over the normalized name: uniqueness only applies to a
            // user's ACTIVE profiles, so archiving a parent frees its name for reuse.
            // Created via raw SQL because EF's model API can't express lower(...) indexes.
            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX "IX_ParentProfiles_Owner_NameLower"
                ON "ParentProfiles" ("OwnerUserId", lower("DisplayName"))
                WHERE "IsArchived" = 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_ParentProfiles_Owner_NameLower";""");
        }
    }
}
