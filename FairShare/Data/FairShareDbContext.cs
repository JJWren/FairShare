using FairShare.Models;
using Microsoft.EntityFrameworkCore;

namespace FairShare.Data;

public class FairShareDbContext(DbContextOptions<FairShareDbContext> options) : DbContext(options)
{
    public DbSet<ParentProfile> ParentProfiles => Set<ParentProfile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
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
        });
    }
}
