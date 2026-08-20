using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace FairShare.Api.Observability;

/// <summary>
/// Drains <see cref="SqliteLoggerProvider"/>'s channel and batch-inserts into the Logs table.
/// Writes via raw Microsoft.Data.Sqlite on purpose: an EF write would emit EF log events and
/// re-enter the logger. Failures fall back to stderr - the sink must never throw into the host.
/// </summary>
public sealed class LogSink(SqliteLoggerProvider provider, IConfiguration configuration) : BackgroundService
{
    private const int MaxBatchSize = 100;
    private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(2);

    private readonly ChannelReader<LogRow> _reader = provider.Reader;
    private readonly string _connectionString = configuration.GetConnectionString("Default")
        ?? throw new InvalidOperationException("Connection string not found.");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        List<LogRow> batch = new(MaxBatchSize);

        try
        {
            while (await _reader.WaitToReadAsync(stoppingToken))
            {
                // Give a burst a moment to accumulate so it lands as one transaction.
                await Task.Delay(FlushInterval, stoppingToken);
                DrainInto(batch);
                Flush(batch);
            }
        }
        catch (OperationCanceledException)
        {
            // Shutdown: fall through to the final drain below.
        }

        DrainInto(batch);
        Flush(batch);
    }

    private void DrainInto(List<LogRow> batch)
    {
        while (batch.Count < MaxBatchSize * 10 && _reader.TryRead(out LogRow row))
        {
            batch.Add(row);
        }
    }

    private void Flush(List<LogRow> batch)
    {
        if (batch.Count == 0)
        {
            return;
        }

        try
        {
            using SqliteConnection connection = new(_connectionString);
            connection.Open();
            using SqliteTransaction transaction = connection.BeginTransaction();
            using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                "INSERT INTO Logs (OccurredAtUtc, Level, Category, Message, Exception) " +
                "VALUES ($occurredAt, $level, $category, $message, $exception);";

            SqliteParameter occurredAt = command.Parameters.Add("$occurredAt", SqliteType.Text);
            SqliteParameter level = command.Parameters.Add("$level", SqliteType.Integer);
            SqliteParameter category = command.Parameters.Add("$category", SqliteType.Text);
            SqliteParameter message = command.Parameters.Add("$message", SqliteType.Text);
            SqliteParameter exception = command.Parameters.Add("$exception", SqliteType.Text);

            foreach (LogRow row in batch)
            {
                // Pass the DateTime itself: Microsoft.Data.Sqlite then stores the exact text
                // format EF Core uses, so ordered comparisons (retention purge cutoffs) stay
                // valid across rows written here and read through the EF entity.
                occurredAt.Value = row.OccurredAtUtc;
                level.Value = row.Level;
                category.Value = row.Category;
                message.Value = row.Message;
                exception.Value = (object?)row.Exception ?? DBNull.Value;
                command.ExecuteNonQuery();
            }

            transaction.Commit();
        }
        catch (Exception ex)
        {
            // stderr, not ILogger: logging a log-sink failure through the sink would loop.
            Console.Error.WriteLine($"FairShare log sink flush failed ({batch.Count} rows dropped): {ex.Message}");
        }
        finally
        {
            batch.Clear();
        }
    }
}
