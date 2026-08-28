using System.IO;
using System.IO.Compression;
using FairShare.Api.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;

namespace FairShare.Tests.Api;

/// <summary>
/// Startup safety (#152): a corrupt or unmigratable database refuses to serve instead of
/// answering requests against a broken schema, and the pre-migration snapshot uses the
/// SQLite online-backup API so it is consistent and restorable.
/// </summary>
[Collection("Api")]
public class StartupSafetyTests
{
    [Fact]
    public void CorruptDatabase_RefusesToStart()
    {
        string dbPath = Path.Combine(Path.GetTempPath(), $"fairshare-corrupt-{Guid.NewGuid():N}.db");
        File.WriteAllText(dbPath, "this is not a sqlite database");

        try
        {
            using CorruptDbFactory factory = new(dbPath);

            // Host startup runs the integrity check / migration sequence, which must
            // throw rather than let the app come up over a broken file.
            Assert.ThrowsAny<Exception>(() => factory.CreateClient());
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(dbPath);
        }
    }

    [Fact]
    public void PreMigrationSnapshot_IsRestorable()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"fairshare-backup-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        string dbPath = Path.Combine(dir, "source.db");
        string backupDir = Path.Combine(dir, "backups");

        try
        {
            using (SqliteConnection seed = new($"Data Source={dbPath};Pooling=False"))
            {
                seed.Open();
                using SqliteCommand create = seed.CreateCommand();
                create.CommandText = "CREATE TABLE t (x INTEGER); INSERT INTO t VALUES (1), (2), (3);";
                create.ExecuteNonQuery();
            }

            SqliteBackup.CreateSnapshot(dbPath, backupDir);

            string zip = Assert.Single(Directory.GetFiles(backupDir, "*.zip"));
            string extractDir = Path.Combine(dir, "extracted");
            ZipFile.ExtractToDirectory(zip, extractDir);
            string restored = Assert.Single(Directory.GetFiles(extractDir, "*.db"));

            using SqliteConnection check = new($"Data Source={restored};Mode=ReadOnly;Pooling=False");
            check.Open();
            using SqliteCommand count = check.CreateCommand();
            count.CommandText = "SELECT COUNT(*) FROM t;";
            Assert.Equal(3L, count.ExecuteScalar());
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private sealed class CorruptDbFactory : FairShareApiFactory
    {
        private readonly string _dbPath;

        public CorruptDbFactory(string dbPath)
        {
            _dbPath = dbPath;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            // Later SetEnvVar wins over the base fixture's own temp file.
            SetEnvVar("ConnectionStrings__Default", $"Data Source={_dbPath}");
        }
    }
}
