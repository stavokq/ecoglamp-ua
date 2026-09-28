using Microsoft.EntityFrameworkCore;
using WebLab.Models;

namespace WebLab.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Region> Regions => Set<Region>();
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<GlampingSite> GlampingSites => Set<GlampingSite>();
    public DbSet<GlampingActivity> GlampingActivities => Set<GlampingActivity>();
    public DbSet<Review> Reviews => Set<Review>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<GlampingActivity>()
            .HasKey(ga => new { ga.GlampingSiteId, ga.ActivityId });

        modelBuilder.Entity<GlampingActivity>()
            .HasOne(ga => ga.GlampingSite)
            .WithMany(g => g.GlampingActivities)
            .HasForeignKey(ga => ga.GlampingSiteId);

        modelBuilder.Entity<GlampingActivity>()
            .HasOne(ga => ga.Activity)
            .WithMany(a => a.GlampingActivities)
            .HasForeignKey(ga => ga.ActivityId);

        modelBuilder.Entity<GlampingSite>()
            .HasOne(g => g.Region)
            .WithMany(r => r.GlampingSites)
            .HasForeignKey(g => g.RegionId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Review>()
            .HasOne(r => r.GlampingSite)
            .WithMany(g => g.Reviews)
            .HasForeignKey(r => r.GlampingSiteId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
