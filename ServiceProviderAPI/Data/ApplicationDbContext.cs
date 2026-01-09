using Microsoft.EntityFrameworkCore;
using ServiceProviderAPI.Models;

namespace ServiceProviderAPI.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Pro> Pros { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<Service> Services { get; set; }
    public DbSet<ProUser> ProUsers { get; set; }
    public DbSet<VerificationCode> VerificationCodes { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ProUser>()
            .HasKey(pu => new { pu.ProId, pu.UserId });

        modelBuilder.Entity<ProUser>()
            .HasOne(pu => pu.Pro)
            .WithMany(p => p.ProUsers)
            .HasForeignKey(pu => pu.ProId);

        modelBuilder.Entity<ProUser>()
            .HasOne(pu => pu.User)
            .WithMany(u => u.ProUsers)
            .HasForeignKey(pu => pu.UserId);

        modelBuilder.Entity<Service>()
            .HasOne(s => s.Pro)
            .WithMany(p => p.Services)
            .HasForeignKey(s => s.ProId);

        modelBuilder.Entity<Service>()
            .Property(s => s.Price)
            .HasPrecision(18, 2);
    }
}
