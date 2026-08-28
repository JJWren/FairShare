using System;
using System.IO;
using System.IO.Compression;
using Microsoft.Data.Sqlite;

namespace FairShare.Api.Persistence;

/// <summary>
/// Pre-migration snapshot of the SQLite database, zipped next to it. Uses SQLite's
/// online-backup API - a raw file copy can capture a torn state mid-write; the backup
/// API takes a consistent snapshot under the database's own locking.
/// </summary>
public static class SqliteBackup
{
    // The privacy page promises deleted data does not linger indefinitely in backup
    // snapshots (it says "about 30 days"); this retention makes that sentence true.
    private const int RetentionDays = 30;

    public static void CreateSnapshot(string dbPath, string backupDir)
    {
        if (!File.Exists(dbPath))
        {
            return;
        }

        Directory.CreateDirectory(backupDir);
        PruneExpiredSnapshots(backupDir);

        // Timestamp alone collides when two instances start in the same second (e.g.
        // parallel test hosts); the random suffix keeps every backup name unique.
        string stamp = $"{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}"[..24];
        string backupFile = Path.Combine(backupDir, $"fairshare_{stamp}.db");

        // Pooling off so no held handle blocks deleting the intermediate file below.
        using (SqliteConnection source = new($"Data Source={dbPath};Mode=ReadOnly;Pooling=False"))
        using (SqliteConnection destination = new($"Data Source={backupFile};Pooling=False"))
        {
            source.Open();
            destination.Open();
            source.BackupDatabase(destination);
        }

        string zipPath = backupFile + ".zip";
        using ZipArchive zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        zip.CreateEntryFromFile(backupFile, Path.GetFileName(backupFile));
        File.Delete(backupFile);
    }

    private static void PruneExpiredSnapshots(string backupDir)
    {
        DateTime cutoffUtc = DateTime.UtcNow.AddDays(-RetentionDays);

        foreach (FileInfo stale in new DirectoryInfo(backupDir).GetFiles("fairshare_*.zip"))
        {
            if (stale.LastWriteTimeUtc < cutoffUtc)
            {
                stale.Delete();
            }
        }
    }
}
