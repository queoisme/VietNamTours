namespace GuideMarket.Api.Repositories;

public interface IUnitOfWork : IDisposable
{
    IUserRepository Users { get; }
    ITourRepository Tours { get; }
    IGuideProfileRepository GuideProfiles { get; }
    IGuideApplicationRepository GuideApplications { get; }
    IBookingRepository Bookings { get; }
    INotificationRepository Notifications { get; }
    IReviewRepository Reviews { get; }
    IWishlistRepository Wishlists { get; }
    IConversationRepository Conversations { get; }
    IBoostRepository Boosts { get; }
    ISubscriptionRepository Subscriptions { get; }
    ISubscriptionPlanConfigRepository SubscriptionPlanConfigs { get; }
    IBoostPlanConfigRepository BoostPlanConfigs { get; }
    IWithdrawalRepository Withdrawals { get; }
    Task<int> SaveChangesAsync();
}
