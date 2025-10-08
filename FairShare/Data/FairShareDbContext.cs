using FairShare.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FairShare.Data;

public class FairShareDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public FairShareDbContext(DbContextOptions<FairShareDbContext> options) : base(options) { }

    public DbSet<ParentProfile> ParentProfiles => Set<ParentProfile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder); // Keep this first when using Identity

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
