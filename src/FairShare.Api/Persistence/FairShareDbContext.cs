using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System;
using FairShare.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FairShare.Api.Persistence;

public class FairShareDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public FairShareDbContext(DbContextOptions<FairShareDbContext> options) : base(options) { }

    public DbSet<ParentProfile> ParentProfiles => Set<ParentProfile>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder); // Keep this first when using Identity

        modelBuilder.Entity<RefreshToken>(b =>
        {
            b.HasKey(t => t.Id);
            b.Property(t => t.TokenHash).IsRequired();
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
    }
}







