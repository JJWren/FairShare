using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System;
using FairShare.Api.Models;
using FairShare.Api.Observability;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FairShare.Api.Persistence;

public class FairShareDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public FairShareDbContext(DbContextOptions<FairShareDbContext> options) : base(options) { }

    public DbSet<ParentProfile> ParentProfiles => Set<ParentProfile>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<LogEntry> Logs => Set<LogEntry>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<PageView> PageViews => Set<PageView>();
    public DbSet<AnalyticsEvent> AnalyticsEvents => Set<AnalyticsEvent>();
    public DbSet<AnalyticsState> AnalyticsStates => Set<AnalyticsState>();
    public DbSet<DailySiteStat> DailySiteStats => Set<DailySiteStat>();
    public DbSet<DailyRouteStat> DailyRouteStats => Set<DailyRouteStat>();
    public DbSet<DailyReferrerStat> DailyReferrerStats => Set<DailyReferrerStat>();
    public DbSet<DailyEventStat> DailyEventStats => Set<DailyEventStat>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder); // Keep this first when using Identity

        modelBuilder.Entity<RefreshToken>(b =>
        {
            b.HasKey(t => t.Id);
            b.Property(t => t.TokenHash).IsRequired();
            // Default TRUE so rows minted before remember-this-device existed keep the
            // 30-day behavior they were issued with - the migration must not downgrade
            // live sessions.
            b.Property(t => t.IsPersistent).HasDefaultValue(true);
            b.HasIndex(t => t.TokenHash).IsUnique();
            b.HasIndex(t => t.UserId);
        });

        modelBuilder.Entity<ParentProfile>(b =>
        {
            b.HasKey(p => p.Id);
            b.HasIndex(p => p.DisplayName);
            b.Property(p => p.DisplayName).HasMaxLength(100).IsRequired();
            b.Property(p => p.RowVersion).IsRowVersion();
            b.Property(p => p.CreatedUtc).HasDefaultValueSql("CURRENT_TIMESTAMP");

            b.HasIndex(p => new
            {
                p.MonthlyGrossIncome,
                p.PreexistingChildSupport,
                p.PreexistingAlimony,
                p.WorkRelatedChildcareCosts,
                p.HealthcareCoverageCosts,
                p.HasPrimaryCustody
            }).HasDatabaseName("IX_ParentProfile_DuplicateSig");

            b.HasIndex(p => p.OwnerUserId);
            b.HasOne<ApplicationUser>()
             .WithMany()
             .HasForeignKey(p => p.OwnerUserId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<LogEntry>(b =>
        {
            b.HasKey(l => l.Id);
            b.HasIndex(l => l.OccurredAtUtc);
            b.Property(l => l.Category).HasMaxLength(LogEntry.MaxCategoryLength).IsRequired();
            b.Property(l => l.Message).HasMaxLength(LogEntry.MaxMessageLength).IsRequired();
            b.Property(l => l.Exception).HasMaxLength(LogEntry.MaxExceptionLength);
        });

        modelBuilder.Entity<AuditEvent>(b =>
        {
            b.HasKey(a => a.Id);
            b.HasIndex(a => a.OccurredAtUtc);
            b.Property(a => a.ActorName).HasMaxLength(AuditEvent.MaxActorNameLength);
            b.Property(a => a.Action).HasMaxLength(AuditEvent.MaxActionLength).IsRequired();
            b.Property(a => a.Target).HasMaxLength(AuditEvent.MaxTargetLength);
            b.Property(a => a.Detail).HasMaxLength(AuditEvent.MaxDetailLength);
        });

        modelBuilder.Entity<PageView>(b =>
        {
            b.HasKey(v => v.Id);
            b.HasIndex(v => v.OccurredAtUtc);
            b.Property(v => v.Path).HasMaxLength(PageView.MaxPathLength).IsRequired();
            b.Property(v => v.ReferrerHost).HasMaxLength(PageView.MaxReferrerHostLength);
            b.Property(v => v.VisitorKey).HasMaxLength(PageView.MaxVisitorKeyLength).IsRequired();
        });

        modelBuilder.Entity<AnalyticsEvent>(b =>
        {
            b.HasKey(e => e.Id);
            b.HasIndex(e => e.OccurredAtUtc);
            b.Property(e => e.Name).HasMaxLength(AnalyticsEvent.MaxNameLength).IsRequired();
            b.Property(e => e.Target).HasMaxLength(AnalyticsEvent.MaxTargetLength);
            b.Property(e => e.VisitorKey).HasMaxLength(PageView.MaxVisitorKeyLength).IsRequired();
        });

        modelBuilder.Entity<AnalyticsState>(b =>
        {
            b.HasKey(s => s.Id);
            b.Property(s => s.SecretBase64).HasMaxLength(64).IsRequired();
        });

        modelBuilder.Entity<DailySiteStat>(b =>
        {
            // Day is the primary key AND the rollup watermark (max Day = last completed day).
            b.HasKey(s => s.Day);
        });

        modelBuilder.Entity<DailyRouteStat>(b =>
        {
            b.HasKey(s => s.Id);
            b.Property(s => s.Path).HasMaxLength(PageView.MaxPathLength).IsRequired();
            b.HasIndex(s => new { s.Day, s.Path }).IsUnique();
        });

        modelBuilder.Entity<DailyReferrerStat>(b =>
        {
            b.HasKey(s => s.Id);
            b.Property(s => s.ReferrerHost).HasMaxLength(PageView.MaxReferrerHostLength).IsRequired();
            b.HasIndex(s => new { s.Day, s.ReferrerHost }).IsUnique();
        });

        modelBuilder.Entity<DailyEventStat>(b =>
        {
            b.HasKey(s => s.Id);
            b.Property(s => s.Name).HasMaxLength(AnalyticsEvent.MaxNameLength).IsRequired();
            b.Property(s => s.Target).HasMaxLength(AnalyticsEvent.MaxTargetLength);
            b.HasIndex(s => new { s.Day, s.Name });
        });
    }
}







