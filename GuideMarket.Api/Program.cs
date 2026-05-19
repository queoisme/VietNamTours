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
builder.Services.AddScoped<IWithdrawalRepository, WithdrawalRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

builder.Services.AddSingleton<VnPayClient>();

// HttpClient + Supabase clients
builder.Services.AddHttpClient<SupabaseAuthClient>();
builder.Services.AddHttpClient<SupabaseStorageClient>();

// Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ITourService, TourService>();
builder.Services.AddScoped<IGuideProfileService, GuideProfileService>();
builder.Services.AddScoped<IGuideApplicationService, GuideApplicationService>();
builder.Services.AddScoped<IBookingService, BookingService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<IEmailService, ResendEmailService>();
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

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.UseHangfireDashboard("/hangfire");

var recurringJobs = app.Services.GetRequiredService<IRecurringJobManager>();
recurringJobs.AddOrUpdate<ExpireBoostJob>("expire-boosts", x => x.ExecuteAsync(), Cron.Hourly);
recurringJobs.AddOrUpdate<ExpireSubscriptionJob>("expire-subscriptions", x => x.ExecuteAsync(), Cron.Daily);

app.MapGet("/", () => Results.Ok(new { name = "GuideMarket API", status = "ok" }));
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapControllers();

app.Run();
