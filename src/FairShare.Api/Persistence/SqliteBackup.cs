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
    public static void CreateSnapshot(string dbPath, string backupDir)
    {
        if (!File.Exists(dbPath))
        {
            return;
        }

        Directory.CreateDirectory(backupDir);

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
}
