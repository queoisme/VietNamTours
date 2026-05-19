using GuideMarket.Api.Data;

namespace GuideMarket.Api.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _db;
    public IUserRepository Users { get; }
    public ITourRepository Tours { get; }
    public IGuideProfileRepository GuideProfiles { get; }
    public IGuideApplicationRepository GuideApplications { get; }
    public IBookingRepository Bookings { get; }
    public INotificationRepository Notifications { get; }
    public IReviewRepository Reviews { get; }
    public IWishlistRepository Wishlists { get; }
    public IConversationRepository Conversations { get; }
    public IBoostRepository Boosts { get; }
    public ISubscriptionRepository Subscriptions { get; }
    public IWithdrawalRepository Withdrawals { get; }

    public UnitOfWork(
        AppDbContext db,
        IUserRepository users,
        ITourRepository tours,
        IGuideProfileRepository guideProfiles,
        IGuideApplicationRepository guideApplications,
        IBookingRepository bookings,
        INotificationRepository notifications,
        IReviewRepository reviews,
        IWishlistRepository wishlists,
        IConversationRepository conversations,
        IBoostRepository boosts,
        ISubscriptionRepository subscriptions,
        IWithdrawalRepository withdrawals)
    {
        _db = db;
        Users = users;
        Tours = tours;
        GuideProfiles = guideProfiles;
        GuideApplications = guideApplications;
        Bookings = bookings;
        Notifications = notifications;
        Reviews = reviews;
        Wishlists = wishlists;
        Conversations = conversations;
        Boosts = boosts;
        Subscriptions = subscriptions;
        Withdrawals = withdrawals;
    }

    public Task<int> SaveChangesAsync() => _db.SaveChangesAsync();

    public void Dispose() => _db.Dispose();
}
