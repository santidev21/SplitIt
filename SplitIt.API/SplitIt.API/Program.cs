using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SplitIt.API;
using SplitIt.API.Middleware;
using SplitIt.Domain.Entities;
using SplitIt.Infrastructure.Persistence;
using SplitIt.Infrastructure.Services;
using System.Text;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Config validation — fail fast if secrets missing
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings.GetValue<string>("SecretKey");
var jwtIssuer = jwtSettings.GetValue<string>("Issuer");
var jwtAudience = jwtSettings.GetValue<string>("Audience");

if (string.IsNullOrWhiteSpace(secretKey) || secretKey.Length < 32)
{
    if (builder.Environment.IsProduction())
        throw new InvalidOperationException("JwtSettings:SecretKey is missing or too short (min 32 chars). Set via env JwtSettings__SecretKey.");
    // Dev/test: generate ephemeral key so app and tests can start without config
    secretKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    Console.WriteLine("WARNING: JwtSettings:SecretKey not configured. Using ephemeral key (tokens will not survive restart).");
}

if ((string.IsNullOrWhiteSpace(jwtIssuer) || string.IsNullOrWhiteSpace(jwtAudience)) && builder.Environment.IsProduction())
    throw new InvalidOperationException("JwtSettings:Issuer/Audience required in Production.");

var effectiveSecret = secretKey;
var effectiveIssuer = string.IsNullOrWhiteSpace(jwtIssuer) ? "https://localhost" : jwtIssuer;
var effectiveAudience = string.IsNullOrWhiteSpace(jwtAudience) ? "https://localhost" : jwtAudience;
var keyBytes = Encoding.UTF8.GetBytes(effectiveSecret);

// Services
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddInfrastructure(builder.Configuration);

// DataProtection — persist keys to volume (survives docker compose down/up), not in Git
var keysPath = "/home/app/.aspnet/DataProtection-Keys";
try
{
    Directory.CreateDirectory(keysPath);
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
        .SetApplicationName("SplitIt");
}
catch (Exception ex)
{
    Console.WriteLine($"DataProtection persistence warning: {ex.Message}");
}

// Health checks — liveness (process) vs readiness (DB)
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: new[] { "live" })
    .AddDbContextCheck<AppDbContext>("db", tags: new[] { "ready" });

// Password hasher
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

// App services
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<GroupService>();
builder.Services.AddScoped<CurrenciesService>();
builder.Services.AddScoped<UsersService>();
builder.Services.AddScoped<ExpensesService>();
builder.Services.AddScoped<FriendshipsService>();
builder.Services.AddScoped<SettingsService>();

// Exception handling
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Rate limiting — auth endpoints: 5 requests per minute per IP, general API moderate
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = 429;
        await context.HttpContext.Response.WriteAsJsonAsync(new { message = "Too many requests. Please try again later." }, token);
    };

    // Strict for login/register
    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? httpContext.Request.Headers.Host.ToString(),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));

    // Moderate for general API
    options.AddFixedWindowLimiter("fixed", opt =>
    {
        opt.PermitLimit = 100;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueLimit = 0;
    });
});

// JWT — unified UTF8, ClockSkew Zero, strict validation
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    options.SaveToken = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidIssuer = effectiveIssuer,
        ValidAudience = effectiveAudience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero,
        RequireSignedTokens = true,
        RequireExpirationTime = true,
        ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 }
    };
    // Explicitly reject 'alg:none' is default, but enforce algorithm check via event
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = context =>
        {
            var alg = context.SecurityToken is Microsoft.IdentityModel.JsonWebTokens.JsonWebToken jwt ? jwt.Alg : null;
            if (alg != null && !string.Equals(alg, SecurityAlgorithms.HmacSha256, StringComparison.Ordinal))
            {
                context.Fail($"Invalid token algorithm: {alg}");
            }
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();

// Swagger
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: 'Bearer YOUR_TOKEN'",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            new string[] {}
        }
    });
});

// CORS — env-based, no AllowAnyOrigin in production
var allowedOriginsRaw = builder.Configuration.GetValue<string>("Cors:AllowedOrigins") ?? builder.Configuration.GetValue<string>("Cors__AllowedOrigins") ?? "";
var allowedOrigins = allowedOriginsRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .Where(o => !string.IsNullOrWhiteSpace(o)).ToArray();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AppCors", policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins)
                  .WithHeaders("Authorization", "Content-Type", "Accept")
                  .WithMethods("GET", "POST", "PUT", "DELETE")
                  .AllowCredentials();
        }
        else if (builder.Environment.IsDevelopment())
        {
            // Dev default — only localhost:4200, not AnyOrigin
            policy.WithOrigins("http://localhost:4200", "https://localhost:4200")
                  .WithHeaders("Authorization", "Content-Type", "Accept")
                  .WithMethods("GET", "POST", "PUT", "DELETE")
                  .AllowCredentials();
        }
        else
        {
            // Production without config — deny all (fail closed)
            policy.WithOrigins(Array.Empty<string>());
        }
    });
});

var app = builder.Build();

// Phase 10: dedicated migration job — handle --migrate flag before starting web server
if (args.Contains("--migrate"))
{
    Console.WriteLine("=== SplitIt migrator: applying EF Core migrations ===");
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        var canConnect = await db.Database.CanConnectAsync();
        Console.WriteLine($"Database reachable: {canConnect}");
        if (!canConnect)
        {
            Console.WriteLine("ERROR: Cannot connect to database.");
            Environment.Exit(1);
            return;
        }

        // Debug: show what migrations EF sees in the assembly
        var allMigrations = db.Database.GetMigrations().ToList();
        Console.WriteLine($"Migrations found in assembly: {allMigrations.Count}");
        foreach (var m in allMigrations) Console.WriteLine($"  - {m}");

        var pending = db.Database.GetPendingMigrations().ToList();
        Console.WriteLine($"Pending migrations: {(pending.Count == 0 ? "(none)" : string.Join(", ", pending))}");

        db.Database.Migrate();

        var applied = db.Database.GetAppliedMigrations().ToList();
        Console.WriteLine($"Applied migrations count: {applied.Count} — last: {applied.LastOrDefault()}");
        Console.WriteLine("Migrations applied successfully — exiting migrator");
        return;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"MIGRATION FAILED: {ex}");
        Environment.Exit(1);
        return;
    }
}

// Middleware pipeline
// Trust X-Forwarded-* headers only from internal Docker networks.
// The reverse proxy is the only path to the backend; this preserves the original client IP and HTTPS scheme.
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost,
    KnownNetworks =
    {
        new Microsoft.AspNetCore.HttpOverrides.IPNetwork(System.Net.IPAddress.Parse("10.0.0.0"), 8),
        new Microsoft.AspNetCore.HttpOverrides.IPNetwork(System.Net.IPAddress.Parse("172.16.0.0"), 12),
        new Microsoft.AspNetCore.HttpOverrides.IPNetwork(System.Net.IPAddress.Parse("192.168.0.0"), 16),
        new Microsoft.AspNetCore.HttpOverrides.IPNetwork(System.Net.IPAddress.Parse("127.0.0.1"), 32)
    },
    // Do not trust arbitrary proxies; only the explicitly known private ranges above.
    ForwardLimit = 1
});

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    // Production: HSTS (if behind proxy, nginx will add HSTS as well)
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseCors("AppCors");
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Health endpoints — minimal response, no sensitive details
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = r => r.Tags.Contains("live"),
    ResponseWriter = async (ctx, report) =>
    {
        ctx.Response.ContentType = "text/plain";
        await ctx.Response.WriteAsync(report.Status == HealthStatus.Healthy ? "Healthy" : "Unhealthy");
    }
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = r => r.Tags.Contains("ready"),
    ResponseWriter = async (ctx, report) =>
    {
        ctx.Response.ContentType = "text/plain";
        await ctx.Response.WriteAsync(report.Status == HealthStatus.Healthy ? "Healthy" : "Unhealthy");
    }
});
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => true,
    ResponseWriter = async (ctx, report) =>
    {
        ctx.Response.ContentType = "text/plain";
        await ctx.Response.WriteAsync(report.Status == HealthStatus.Healthy ? "Healthy" : "Unhealthy");
    }
});

app.Run();

// For integration tests
public partial class Program { }
