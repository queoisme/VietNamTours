using FluentValidation;
using FluentValidation.AspNetCore;
using GuideMarket.Api.Data;
using GuideMarket.Api.Infrastructure;
using GuideMarket.Api.Middleware;
using GuideMarket.Api.Models;
using GuideMarket.Api.Repositories;
using GuideMarket.Api.Services;
using GuideMarket.Api.Services.Interfaces;
using GuideMarket.Api.BackgroundJobs;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Railway (and many PaaS) provide the HTTP port via PORT env var.
// Configure Kestrel to listen on that port when present.
var portEnv = Environment.GetEnvironmentVariable("PORT");
if (int.TryParse(portEnv, out var port) && port > 0)
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

// Serilog
builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration));

// EF Core + PostgreSQL
// Npgsql key-value format required; convert URI if needed (same issue as Hangfire)
var rawConnStrEf = builder.Configuration.GetConnectionString("DefaultConnection")!;
var efConnStr = rawConnStrEf.StartsWith("postgresql://") || rawConnStrEf.StartsWith("postgres://")
    ? ConvertUriToNpgsql(rawConnStrEf)
    : rawConnStrEf;

// Build NpgsqlDataSource with native PG enum mappings (must match DB ENUM type names)
var npgsqlBuilder = new NpgsqlDataSourceBuilder(efConnStr);
npgsqlBuilder.MapEnum<UserRole>("user_role");
npgsqlBuilder.MapEnum<TourCategory>("tour_category");
npgsqlBuilder.MapEnum<TourStatus>("tour_status");
npgsqlBuilder.MapEnum<VerificationStatus>("verification_status");
npgsqlBuilder.MapEnum<SubscriptionPlan>("subscription_plan");
npgsqlBuilder.MapEnum<ApplicationStatus>("application_status");
npgsqlBuilder.MapEnum<BookingStatus>("booking_status");
npgsqlBuilder.MapEnum<PaymentStatus>("payment_status");
npgsqlBuilder.MapEnum<CancellationBy>("cancellation_by");
npgsqlBuilder.MapEnum<BoostPlan>("boost_plan");
npgsqlBuilder.MapEnum<BoostStatus>("boost_status");
npgsqlBuilder.MapEnum<WithdrawalMethod>("withdrawal_method");
npgsqlBuilder.MapEnum<WithdrawalStatus>("withdrawal_status");
var npgsqlDataSource = npgsqlBuilder.Build();

builder.Services.AddDbContext<AppDbContext>(opt => opt.UseNpgsql(npgsqlDataSource));

// Repositories & UnitOfWork
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ITourRepository, TourRepository>();
builder.Services.AddScoped<IGuideProfileRepository, GuideProfileRepository>();
builder.Services.AddScoped<IGuideApplicationRepository, GuideApplicationRepository>();
builder.Services.AddScoped<IBookingRepository, BookingRepository>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<IReviewRepository, ReviewRepository>();
builder.Services.AddScoped<IWishlistRepository, WishlistRepository>();
builder.Services.AddScoped<IConversationRepository, ConversationRepository>();
builder.Services.AddScoped<IBoostRepository, BoostRepository>();
builder.Services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
builder.Services.AddScoped<ISubscriptionPlanConfigRepository, SubscriptionPlanConfigRepository>();
builder.Services.AddScoped<IBoostPlanConfigRepository, BoostPlanConfigRepository>();
builder.Services.AddScoped<IWithdrawalRepository, WithdrawalRepository>();
builder.Services.AddScoped<IOtpRepository, OtpRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

builder.Services.AddSingleton<MomoClient>();
builder.Services.AddSingleton<VnPayClient>();

// HttpClient + Supabase clients
builder.Services.AddHttpClient<SupabaseAuthClient>();
builder.Services.AddHttpClient<SupabaseStorageClient>();

// Services
builder.Services.AddScoped<IOtpService, OtpService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ITourService, TourService>();
builder.Services.AddScoped<IGuideProfileService, GuideProfileService>();
builder.Services.AddScoped<IGuideApplicationService, GuideApplicationService>();
builder.Services.AddScoped<IBookingService, BookingService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IAdminService, AdminService>();
// Read SMTP config: env var trực tiếp (Railway) hoặc từ config system
var smtpHost = Environment.GetEnvironmentVariable("SMTP_HOST")
               ?? Environment.GetEnvironmentVariable("Smtp__Host")
               ?? builder.Configuration["Smtp:Host"];
var brevoApiKey = Environment.GetEnvironmentVariable("BREVO_API_KEY")
                 ?? builder.Configuration["Brevo:ApiKey"];
var sendGridKey = Environment.GetEnvironmentVariable("SENDGRID_API_KEY")
                  ?? builder.Configuration["SendGrid:ApiKey"];
var emailProvider = !string.IsNullOrWhiteSpace(brevoApiKey) ? "BrevoAPI"
    : !string.IsNullOrWhiteSpace(smtpHost) ? "SMTP"
    : !string.IsNullOrWhiteSpace(sendGridKey) ? "SendGrid"
    : "Resend";
Console.WriteLine($"[EMAIL] Provider selected: {emailProvider} | BrevoApiKeyConfigured={!string.IsNullOrWhiteSpace(brevoApiKey)} | Smtp:Host={smtpHost ?? "(null)"}");
builder.Services.AddScoped<IEmailService>(sp =>
{
    if (!string.IsNullOrWhiteSpace(brevoApiKey))
        return new BrevoApiEmailService(sp.GetRequiredService<IHttpClientFactory>(), builder.Configuration);

    if (!string.IsNullOrWhiteSpace(smtpHost))
        return new SmtpEmailService(builder.Configuration);

    if (!string.IsNullOrWhiteSpace(sendGridKey))
        return new SendGridEmailService(builder.Configuration);

    return new ResendEmailService(sp.GetRequiredService<IHttpClientFactory>(), builder.Configuration);
});
builder.Services.AddScoped<IReviewService, ReviewService>();
builder.Services.AddScoped<IWishlistService, WishlistService>();
builder.Services.AddScoped<IConversationService, ConversationService>();
builder.Services.AddScoped<IBoostService, BoostService>();
builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
builder.Services.AddScoped<IWithdrawalService, WithdrawalService>();

// FluentValidation
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// JWT Authentication (Supabase)
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Jwt:Authority"];
        options.Audience = builder.Configuration["Jwt:Audience"];
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
        };
    });

builder.Services.AddAuthorization();

// CORS
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(opt =>
    opt.AddDefaultPolicy(policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()));

// Hangfire (basic setup — jobs implemented later)
// Hangfire requires key-value format; convert URI if needed
var rawConnStr = builder.Configuration.GetConnectionString("DefaultConnection")!;
var hangfireConnStr = rawConnStr.StartsWith("postgresql://") || rawConnStr.StartsWith("postgres://")
    ? ConvertUriToNpgsql(rawConnStr)
    : rawConnStr;

builder.Services.AddHangfire(cfg => cfg
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(c => c.UseNpgsqlConnection(hangfireConnStr)));
// Limit workers to 3 — Supabase free tier pooler caps at 15 connections (shared with EF Core)
builder.Services.AddHangfireServer(opt => opt.WorkerCount = 3);

static string ConvertUriToNpgsql(string uri)
{
    var u = new Uri(uri);
    var userInfo = u.UserInfo.Split(':', 2);
    return $"Host={u.Host};Port={u.Port};Database={u.AbsolutePath.TrimStart('/')};Username={userInfo[0]};Password={userInfo[1]}";
}

// Controllers + Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "GuideMarket API", Version = "v1" });

    // Prevent schema ID conflicts: use fully-qualified names (e.g. ApiResponse<List<X>> vs ApiResponse<X>)
    c.CustomSchemaIds(type => type.FullName!.Replace("+", "."));

    // DateOnly / DateOnly? need explicit registration so Swashbuckle doesn't attempt
    // to reflect them as complex structs (causes 500 when both nullable and non-nullable
    // variants appear across route params, query params, and response body properties)
    c.MapType<DateOnly>(() => new Microsoft.OpenApi.Models.OpenApiSchema
        { Type = "string", Format = "date" });
    c.MapType<DateOnly?>(() => new Microsoft.OpenApi.Models.OpenApiSchema
        { Type = "string", Format = "date", Nullable = true });

    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
    });
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                    { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            []
        }
    });
});

var app = builder.Build();

// Middleware pipeline
app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseSerilogRequestLogging();

var swaggerEnabled =
    app.Environment.IsDevelopment()
    || builder.Configuration.GetValue<bool>("Swagger:Enabled")
    || string.Equals(Environment.GetEnvironmentVariable("ENABLE_SWAGGER"), "true", StringComparison.OrdinalIgnoreCase);

if (swaggerEnabled)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.UseHangfireDashboard("/hangfire");

var recurringJobs = app.Services.GetRequiredService<IRecurringJobManager>();
recurringJobs.AddOrUpdate<ExpireBoostJob>("expire-boosts", x => x.ExecuteAsync(), Cron.Hourly);
recurringJobs.AddOrUpdate<ExpireSubscriptionJob>("expire-subscriptions", x => x.ExecuteAsync(), Cron.Daily);
recurringJobs.AddOrUpdate<CleanupExpiredOtpJob>("cleanup-expired-otp", x => x.ExecuteAsync(), Cron.Hourly);
recurringJobs.AddOrUpdate<BoostExpiringWarningJob>("boost-expiring-warning", x => x.ExecuteAsync(), Cron.Hourly);
recurringJobs.AddOrUpdate<SubscriptionExpiringWarningJob>("subscription-expiring-warning", x => x.ExecuteAsync(), Cron.Daily);

app.MapGet("/", () => Results.Ok(new { name = "GuideMarket API", status = "ok" }));
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapControllers();

app.Run();
