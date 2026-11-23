using NexaHR.Infrastructure.Services.Company;
using NexaHR.Application.Interfaces.Company;
using NexaHR.Infrastructure.Services.Invitations;
using NexaHR.Application.Interfaces.Invitations;
using NexaHR.Infrastructure.Services.Accounts;
using NexaHR.Application.Interfaces.Accounts;
using NexaHR.Infrastructure.Services.Profile;
using NexaHR.Application.Interfaces.Profile;
using NexaHR.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

using Microsoft.EntityFrameworkCore;
using NexaHR.Infrastructure.Persistence;
using NexaHR.Api.Extensions;
using NexaHR.Application.Interfaces.Auth;
using NexaHR.Infrastructure.Services.Auth;

var builder = WebApplication.CreateBuilder(args);

// Register API services
builder.Services.AddControllers();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode =
        StatusCodes.Status429TooManyRequests;

    options.AddPolicy("auth-sensitive", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey:
                context.Connection.RemoteIpAddress?.ToString()
                ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

builder.Services.AddOpenApi();

// Register SQL Server database context
var connectionString = builder.Configuration
    .GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "DefaultConnection is not configured.");

builder.Services.AddDbContext<NexaHRDbContext>(options =>
    options.UseSqlServer(connectionString));

// Register Identity and JWT authentication
builder.Services.AddNexaHRAuthentication(
    builder.Configuration);

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IProfileService, ProfileService>();
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<IInvitationService, InvitationService>();
builder.Services.AddScoped<ICompanySettingsService, CompanySettingsService>();
builder.Services.AddScoped<IAuthEmailService, AuthEmailService>();
builder.Services.AddHostedService<AuthEmailWorker>();
builder.Services.AddDataProtection();

builder.Services.AddScoped<JwtTokenService>();

var app = builder.Build();

// Initialize configured application roles
if (app.Configuration.GetValue<bool>(
    "Database:SeedRoles"))
{
    await using var scope =
        app.Services.CreateAsyncScope();

    var roleManager = scope.ServiceProvider
        .GetRequiredService<
            RoleManager<IdentityRole<Guid>>>();

    await RoleSeeder.SeedAsync(roleManager);
}


// Development configuration
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Configure middleware
app.UseHttpsRedirection();

app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
