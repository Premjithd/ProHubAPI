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
    public DbSet<ServiceCategory> ServiceCategories { get; set; }
    public DbSet<AdminUser> AdminUsers { get; set; }
    public DbSet<AdminInvitation> AdminInvitations { get; set; }
    public DbSet<VerificationCode> VerificationCodes { get; set; }
    public DbSet<Job> Jobs { get; set; }
    public DbSet<JobBid> JobBids { get; set; }
    public DbSet<Message> Messages { get; set; }
    public DbSet<MessageIndex> MessageIndexes { get; set; }
    public DbSet<Payment> Payments { get; set; }  // Phase 1C
    public DbSet<Material> Materials { get; set; }  // Phase 1D
    public DbSet<ServiceArea> ServiceAreas { get; set; }  // Phase 2
    public DbSet<JobInsurance> JobInsurances { get; set; }  // Phase 1E
    public DbSet<JobNotification> JobNotifications { get; set; }  // Phase 1B
    public DbSet<JobCompletion> JobCompletions { get; set; }  // Phase 5

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AdminUser>()
            .HasKey(au => au.Id);

        modelBuilder.Entity<AdminUser>()
            .HasOne(au => au.Pro)
            .WithMany(p => p.AdminUsers)
            .HasForeignKey(au => au.ProId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<AdminUser>()
            .HasOne(au => au.User)
            .WithMany(u => u.AdminUsers)
            .HasForeignKey(au => au.UserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<AdminInvitation>()
            .HasIndex(ai => ai.Token)
            .IsUnique();

        modelBuilder.Entity<AdminInvitation>()
            .HasIndex(ai => ai.Email);

        modelBuilder.Entity<Service>()
            .HasOne(s => s.Pro)
            .WithMany(p => p.Services)
            .HasForeignKey(s => s.ProId);

        modelBuilder.Entity<Service>()
            .Property(s => s.Price)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Job>()
            .HasOne(j => j.User)
            .WithMany()
            .HasForeignKey(j => j.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Job>()
            .HasOne(j => j.AssignedPro)
            .WithMany()
            .HasForeignKey(j => j.AssignedProId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<JobBid>()
            .HasOne(jb => jb.Job)
            .WithMany()
            .HasForeignKey(jb => jb.JobId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<JobBid>()
            .HasOne(jb => jb.Pro)
            .WithMany()
            .HasForeignKey(jb => jb.ProId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Message>()
            .HasOne(m => m.MessageIndex)
            .WithMany(mi => mi.Messages)
            .HasForeignKey(m => m.MessageIndexId)
            .OnDelete(DeleteBehavior.Cascade);

        // Unique constraint on MessageIndex to ensure only one entry per user pair
        modelBuilder.Entity<MessageIndex>()
            .HasIndex(mi => new { mi.UserId1, mi.UserType1, mi.UserId2, mi.UserType2 })
            .IsUnique()
            .HasDatabaseName("IX_MessageIndex_UserPair");

        // Payment entity configurations (Phase 1C)
        modelBuilder.Entity<Payment>()
            .HasOne(p => p.Job)
            .WithMany()
            .HasForeignKey(p => p.JobId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Payment>()
            .HasOne(p => p.Bid)
            .WithMany()
            .HasForeignKey(p => p.BidId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Payment>()
            .HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Material entity configurations (Phase 1D)
        modelBuilder.Entity<Material>()
            .HasOne(m => m.Category)
            .WithMany()
            .HasForeignKey(m => m.ServiceCategoryId)
            .OnDelete(DeleteBehavior.Cascade);

        // JobInsurance entity configurations (Phase 1E)
        modelBuilder.Entity<JobInsurance>()
            .HasOne(ji => ji.Job)
            .WithMany()
            .HasForeignKey(ji => ji.JobId)
            .OnDelete(DeleteBehavior.Cascade);

        // JobNotification entity configurations (Phase 1B)
        modelBuilder.Entity<JobNotification>()
            .HasOne(jn => jn.Job)
            .WithMany()
            .HasForeignKey(jn => jn.JobId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<JobNotification>()
            .HasOne(jn => jn.Pro)
            .WithMany()
            .HasForeignKey(jn => jn.ProId)
            .OnDelete(DeleteBehavior.Cascade);

        // JobCompletion entity configurations (Phase 5)
        modelBuilder.Entity<JobCompletion>()
            .HasOne(jc => jc.Job)
            .WithMany()
            .HasForeignKey(jc => jc.JobId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
