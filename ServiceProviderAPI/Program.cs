using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ServiceProviderAPI.Data;
using ServiceProviderAPI.Hubs;
using ServiceProviderAPI.Services;
using ServiceProviderAPI.Services.Abstractions;
using ServiceProviderAPI.Services.Channels;
using ServiceProviderAPI.Services.Providers;

var builder = WebApplication.CreateBuilder(args);

Console.WriteLine("🔧 Starting application...");

// Fail fast: JWT key must be set before we wire up any services.
// In development this comes from appsettings.Development.json.
// In production set it via appsettings.Production.json or the ASPNETCORE_ env var override.
var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.StartsWith("REPLACE_ME"))
{
    Console.Error.WriteLine("FATAL: Jwt:Key is not configured. Set it in appsettings.Production.json or via environment variable Jwt__Key.");
    Environment.Exit(1);
}

try
{
    Console.WriteLine("📦 Adding DbContext...");
    // Add services to the container.
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
    {
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
        Console.WriteLine($"📍 Connection string: {(string.IsNullOrEmpty(connectionString) ? "NOT FOUND" : "OK")}");
        options.UseSqlServer(connectionString, sqlOptions =>
        {
            sqlOptions.CommandTimeout(30); // Set 30 second timeout for database commands
        });
    });

    Console.WriteLine("🔐 Adding JWT services...");
    builder.Services.AddScoped<IJwtService, JwtService>();
    builder.Services.AddScoped<ITokenBlacklistService, TokenBlacklistService>();
    builder.Services.AddScoped<IVerificationService, VerificationService>();
    builder.Services.AddScoped<IEmailService, SmtpEmailService>();

    // Phase 1B: Notification services
    Console.WriteLine("📧 Adding notification services...");
    builder.Services.AddScoped<ServiceProviderAPI.Services.Abstractions.IEmailChannel>(sp =>
        new ServiceProviderAPI.Services.Channels.SmtpEmailChannel(
            sp.GetRequiredService<ILogger<ServiceProviderAPI.Services.Channels.SmtpEmailChannel>>(),
            sp.GetRequiredService<IEmailService>()
        )
    );
    builder.Services.AddScoped<ServiceProviderAPI.Services.Abstractions.ISmsChannel>(sp =>
        new ServiceProviderAPI.Services.Channels.Msg91SmsChannel(
            sp.GetRequiredService<ILogger<ServiceProviderAPI.Services.Channels.Msg91SmsChannel>>(),
            builder.Configuration,
            sp.GetRequiredService<HttpClient>()
        )
    );
    builder.Services.AddScoped<ServiceProviderAPI.Services.INotificationService, ServiceProviderAPI.Services.NotificationService>();

    // Phase 1C: Payment services
    Console.WriteLine("💳 Adding payment services...");
    var useMockPayments = builder.Configuration.GetValue<bool>("Payment:UseMockProvider");
    if (useMockPayments)
    {
        Console.WriteLine("⚠️  Using MockPaymentProvider (simulated payments — development only)");
        builder.Services.AddScoped<ServiceProviderAPI.Services.Abstractions.IPaymentProvider>(sp =>
            new ServiceProviderAPI.Services.Providers.MockPaymentProvider(
                sp.GetRequiredService<ILogger<ServiceProviderAPI.Services.Providers.MockPaymentProvider>>()
            )
        );
    }
    else
    {
        builder.Services.AddScoped<ServiceProviderAPI.Services.Abstractions.IPaymentProvider>(sp =>
            new ServiceProviderAPI.Services.Providers.RazorpayPaymentProvider(
                sp.GetRequiredService<ILogger<ServiceProviderAPI.Services.Providers.RazorpayPaymentProvider>>(),
                builder.Configuration,
                sp.GetRequiredService<HttpClient>()
            )
        );
    }
    builder.Services.AddScoped<ServiceProviderAPI.Services.IRateSplitService, ServiceProviderAPI.Services.RateSplitService>();

    // Phase 1E: Insurance service abstraction (no provider implementation yet)
    Console.WriteLine("🛡️ Adding insurance service abstraction...");
    builder.Services.AddScoped<ServiceProviderAPI.Services.Abstractions.IInsuranceProvider>(sp => null!);

    // Phase 5: File storage service (placeholder for future implementation)
    // builder.Services.AddScoped<ServiceProviderAPI.Services.Abstractions.IFileStorageService, ...>();

    // Payout service
    builder.Services.AddScoped<ServiceProviderAPI.Services.IPayoutService, ServiceProviderAPI.Services.PayoutService>();

    // Service area service
    builder.Services.AddScoped<IServiceAreaService, ServiceAreaService>();

    // Inline address geocoding (write path) — shares NominatimThrottle; independent of admin backfill
    builder.Services.AddScoped<ServiceProviderAPI.Services.IGeocodingService, ServiceProviderAPI.Services.GeocodingService>();

    // Seed data service
    Console.WriteLine("🌱 Adding seed data service...");
    builder.Services.AddScoped<SeedDataService>();

    // TODO: Add BidExpirationService (BackgroundService) to proactively mark overdue Pending bids
    // as Expired on a periodic sweep. For now, expiration is enforced only at accept-time in AcceptBid.

    // Add HttpContextAccessor for accessing current HTTP context
    builder.Services.AddHttpContextAccessor();

    // Add HttpClient for external API calls (e.g., Nominatim address service)
    builder.Services.AddHttpClient();
    builder.Services.AddMemoryCache();
    builder.Services.AddSingleton<NominatimThrottle>();

    Console.WriteLine("📡 Adding SignalR...");
    builder.Services.AddSignalR();

    Console.WriteLine("🎮 Adding controllers...");
    // Add controllers with JSON serializer options to handle circular references
    builder.Services.AddControllers()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
            options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase; // Handle camelCase from frontend
            options.JsonSerializerOptions.WriteIndented = true; // For debugging
        });

    Console.WriteLine("🔑 Adding authentication...");
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = builder.Configuration["Jwt:Issuer"],
                ValidAudience = builder.Configuration["Jwt:Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
            };
            options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
            {
                // Allow SignalR WebSocket connections to pass JWT via query string
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    var path = context.HttpContext.Request.Path;
                    if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                    {
                        context.Token = accessToken;
                    }
                    return Task.CompletedTask;
                },
                OnTokenValidated = async context =>
                {
                    var blacklist = context.HttpContext.RequestServices
                        .GetRequiredService<ITokenBlacklistService>();
                    var jti = context.Principal?.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti)?.Value;
                    if (!string.IsNullOrEmpty(jti) && await blacklist.IsRevokedAsync(jti))
                    {
                        context.Fail("Token has been revoked.");
                    }
                }
            };
        });

    Console.WriteLine("📚 Adding Swagger...");
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    Console.WriteLine("🚦 Adding rate limiting...");
    // Auth rate limits are relaxed in Development and Test so local e2e test
    // suites (which log in repeatedly) aren't throttled. Production keeps strict limits.
    var isDevEnv = builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Test");

    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.OnRejected = async (context, token) =>
        {
            context.HttpContext.Response.ContentType = "application/json";
            await context.HttpContext.Response.WriteAsync(
                "{\"message\":\"Too many requests. Please wait before trying again.\"}",
                cancellationToken: token);
        };

        // Login: 5 attempts per minute per IP
        options.AddPolicy("auth-login", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = isDevEnv ? 200 : 5,
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0
                }));

        // Registration: 3 attempts per 5 minutes per IP
        options.AddPolicy("auth-register", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = isDevEnv ? 200 : 3,
                    Window = TimeSpan.FromMinutes(5),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0
                }));

        // Forgot-password: 3 attempts per 15 minutes per IP (prevents enumeration abuse)
        options.AddPolicy("auth-forgot-password", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 3,
                    Window = TimeSpan.FromMinutes(15),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0
                }));
    });

    Console.WriteLine("🌐 Adding CORS...");
    // Allowed origins come from configuration (Cors:AllowedOrigins). Falls back to the
    // local Angular dev server if none are configured.
    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
    if (allowedOrigins is null || allowedOrigins.Length == 0)
        allowedOrigins = new[] { "http://localhost:4200" };

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowAngular", policy =>
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        });
    });

    Console.WriteLine("🏗️ Building application...");
    WebApplication? app = null;
    try
    {
        app = builder.Build();
        Console.WriteLine("✅ Application built successfully");
    }
    catch (Exception buildEx)
    {
        Console.WriteLine($"❌ Error building application: {buildEx.Message}");
        Console.WriteLine($"Stack: {buildEx.StackTrace}");
        if (buildEx.InnerException != null)
        {
            Console.WriteLine($"Inner Exception: {buildEx.InnerException.Message}");
            Console.WriteLine($"Inner Stack: {buildEx.InnerException.StackTrace}");
        }
        throw;
    }

    if (app == null)
    {
        throw new InvalidOperationException("Application builder failed to create app");
    }

    // Apply migrations and seed data
    Console.WriteLine("📦 Applying database migrations...");
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<ApplicationDbContext>();
        
        try
        {
            await context.Database.MigrateAsync();
            Console.WriteLine("✅ Migrations applied successfully");

            // Run seed data
            Console.WriteLine("🌱 Running seed data...");
            try
            {
                var seedService = services.GetRequiredService<SeedDataService>();
                await seedService.SeedAsync();
                Console.WriteLine("✅ Seed data completed");
            }
            catch (Exception seedDataEx)
            {
                Console.WriteLine($"⚠️ Error running seed data: {seedDataEx.Message}");
                // Continue even if seeding fails
            }
        }
        catch (Exception migrationEx)
        {
            Console.WriteLine($"⚠️ Error applying migrations: {migrationEx.Message}");
            Console.WriteLine($"Stack: {migrationEx.StackTrace}");
            // Don't throw - allow app to run even if migrations fail
        }
    }

    // Use CORS
    app.UseCors("AllowAngular");

    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    // Keep HTTPS enforced outside development, but allow emulator HTTP in local dev.
    if (!app.Environment.IsDevelopment())
    {
        app.UseHttpsRedirection();
    }

    app.UseRateLimiter();

    app.UseAuthentication();
    app.UseAuthorization();

    // Maintenance gate — runs after authentication so the admin bypass can see the user's role.
    // When Maintenance:Enabled is true, every request is short-circuited with 503 except the
    // maintenance status, auth (so admins can sign in), and Swagger; Admins are allowed through.
    app.Use(async (context, next) =>
    {
        var config = context.RequestServices.GetRequiredService<IConfiguration>();
        if (!config.GetValue<bool>("Maintenance:Enabled"))
        {
            await next();
            return;
        }

        var path = context.Request.Path.Value ?? string.Empty;
        var allowlisted =
            path.StartsWith("/api/maintenance", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/api/auth", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/health", StringComparison.OrdinalIgnoreCase);

        if (allowlisted || context.User.IsInRole("Admin"))
        {
            await next();
            return;
        }

        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        context.Response.Headers.RetryAfter = "3600";
        await context.Response.WriteAsJsonAsync(new
        {
            maintenance = true,
            message = config["Maintenance:Message"] ?? "The service is temporarily unavailable for maintenance."
        });
    });

    app.MapControllers();
    app.MapHub<NotificationHub>("/hubs/notifications");

    Console.WriteLine("🚀 Starting web host...");
    app.Run();
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Fatal error: {ex.Message}");
    Console.WriteLine($"Stack: {ex.StackTrace}");
    throw;
}
