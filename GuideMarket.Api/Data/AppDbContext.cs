using GuideMarket.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GuideMarket.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Tour> Tours => Set<Tour>();
    public DbSet<TourAvailability> TourAvailabilities => Set<TourAvailability>();
    public DbSet<GuideProfile> GuideProfiles => Set<GuideProfile>();
    public DbSet<GuideApplication> GuideApplications => Set<GuideApplication>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Wishlist> Wishlists => Set<Wishlist>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Boost> Boosts => Set<Boost>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<SubscriptionPlanConfig> SubscriptionPlanConfigs => Set<SubscriptionPlanConfig>();
    public DbSet<Withdrawal> Withdrawals => Set<Withdrawal>();
    public DbSet<OtpVerification> OtpVerifications => Set<OtpVerification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("public");

        // PostgreSQL native enum types
        modelBuilder.HasPostgresEnum<UserRole>("public", "user_role");
        modelBuilder.HasPostgresEnum<TourCategory>("public", "tour_category");
        modelBuilder.HasPostgresEnum<TourStatus>("public", "tour_status");
        modelBuilder.HasPostgresEnum<VerificationStatus>("public", "verification_status");
        modelBuilder.HasPostgresEnum<SubscriptionPlan>("public", "subscription_plan");
        modelBuilder.HasPostgresEnum<ApplicationStatus>("public", "application_status");
        modelBuilder.HasPostgresEnum<BookingStatus>("public", "booking_status");
        modelBuilder.HasPostgresEnum<PaymentStatus>("public", "payment_status");
        modelBuilder.HasPostgresEnum<CancellationBy>("public", "cancellation_by");
        modelBuilder.HasPostgresEnum<BoostPlan>("public", "boost_plan");
        modelBuilder.HasPostgresEnum<BoostStatus>("public", "boost_status");
        modelBuilder.HasPostgresEnum<WithdrawalMethod>("public", "withdrawal_method");
        modelBuilder.HasPostgresEnum<WithdrawalStatus>("public", "withdrawal_status");

        modelBuilder.Entity<User>(e => e.ToTable("users"));

        modelBuilder.Entity<Tour>(e =>
        {
            e.ToTable("tours");
            e.HasOne(t => t.Guide)
             .WithMany()
             .HasForeignKey(t => t.GuideId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TourAvailability>(e =>
        {
            e.ToTable("tour_availabilities");
            e.HasOne(a => a.Tour)
             .WithMany(t => t.Availabilities)
             .HasForeignKey(a => a.TourId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GuideProfile>(e =>
        {
            e.ToTable("guide_profiles");
            e.HasOne(p => p.User)
             .WithMany()
             .HasForeignKey(p => p.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GuideApplication>(e =>
        {
            e.ToTable("guide_applications");
            e.HasOne(a => a.Applicant)
             .WithMany()
             .HasForeignKey(a => a.UserId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(a => a.Reviewer)
             .WithMany()
             .HasForeignKey(a => a.ReviewedBy)
             .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Booking>(e =>
        {
            e.ToTable("bookings");
            e.HasOne(b => b.Tour)
             .WithMany()
             .HasForeignKey(b => b.TourId)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(b => b.Customer)
             .WithMany()
             .HasForeignKey(b => b.CustomerId)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(b => b.Guide)
             .WithMany()
             .HasForeignKey(b => b.GuideId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Conversation>(e =>
        {
            e.ToTable("conversations");
            e.HasOne(c => c.Booking).WithMany().HasForeignKey(c => c.BookingId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(c => c.Customer).WithMany().HasForeignKey(c => c.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(c => c.Guide).WithMany().HasForeignKey(c => c.GuideId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Notification>(e =>
        {
            e.ToTable("notifications");
            e.HasOne(n => n.User)
             .WithMany()
             .HasForeignKey(n => n.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Review>(e =>
        {
            e.ToTable("reviews");
            e.HasOne(r => r.Booking).WithMany().HasForeignKey(r => r.BookingId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(r => r.Tour).WithMany().HasForeignKey(r => r.TourId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.Customer).WithMany().HasForeignKey(r => r.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.Guide).WithMany().HasForeignKey(r => r.GuideId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Wishlist>(e =>
        {
            e.ToTable("wishlists");
            e.HasOne(w => w.Customer).WithMany().HasForeignKey(w => w.CustomerId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(w => w.Tour).WithMany().HasForeignKey(w => w.TourId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Message>(e =>
        {
            e.ToTable("messages");
            e.HasOne(m => m.Conversation).WithMany().HasForeignKey(m => m.ConversationId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(m => m.Sender).WithMany().HasForeignKey(m => m.SenderId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Boost>(e =>
        {
            e.ToTable("boosts");
            e.HasOne(b => b.Tour).WithMany().HasForeignKey(b => b.TourId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(b => b.Guide).WithMany().HasForeignKey(b => b.GuideId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Subscription>(e =>
        {
            e.ToTable("subscriptions");
            e.HasOne(s => s.Guide).WithMany().HasForeignKey(s => s.GuideId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Withdrawal>(e =>
        {
            e.ToTable("withdrawals");
            e.HasOne(w => w.Guide).WithMany().HasForeignKey(w => w.GuideId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SubscriptionPlanConfig>(e =>
        {
            e.ToTable("subscription_plan_configs");
            e.HasKey(p => p.Plan);
            e.Property(p => p.Plan).HasColumnName("plan").HasMaxLength(20).ValueGeneratedNever();
            e.Property(p => p.Price).HasColumnName("price");
            e.Property(p => p.Days).HasColumnName("days");
            e.Property(p => p.Description).HasColumnName("description");
            e.Property(p => p.IsActive).HasColumnName("is_active");
            e.Property(p => p.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<OtpVerification>(e =>
        {
            e.ToTable("otp_verifications");
            e.HasIndex(o => new { o.Target, o.Type })
             .HasFilter("is_used = false")
             .HasDatabaseName("idx_otp_target");
        });
    }
}
