using NexaHR.Application.Interfaces.CvMatching;
using NexaHR.Infrastructure.Services.CvMatching;
using NexaHR.Application.Interfaces.CvExtractions;
using NexaHR.Infrastructure.Services.CvExtractions;
using NexaHR.Application.Interfaces.Applications;
using NexaHR.Application.Interfaces.CvReviews;
using NexaHR.Infrastructure.Services.CvReviews;
using NexaHR.Infrastructure.Services.Applications;
using NexaHR.Application.Interfaces.CvDocuments;
using NexaHR.Infrastructure.Services.CvDocuments;
using NexaHR.Application.Interfaces.Audit;
using NexaHR.Application.Interfaces.Privacy;
using NexaHR.Infrastructure.Services.Audit;
using NexaHR.Infrastructure.Services.Privacy;
using NexaHR.Application.Interfaces.Dashboard;
using NexaHR.Infrastructure.Services.Dashboard;
using NexaHR.Application.Interfaces.Notifications;
using NexaHR.Infrastructure.Services.Notifications;
using NexaHR.Application.Interfaces.Vacancies;
using NexaHR.Infrastructure.Services.Vacancies;
using NexaHR.Application.Interfaces.Departments;
using NexaHR.Infrastructure.Services.Departments;
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

builder.Services.AddScoped<IDepartmentService, DepartmentService>();

builder.Services.AddScoped<IVacancyService, VacancyService>();


builder.Services.AddScoped<INotificationService, NotificationService>();

builder.Services.AddScoped<IDashboardService, DashboardService>();

builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IAuditWriter, AuditWriter>();
builder.Services.AddScoped<IPrivacyService, PrivacyService>();

// Register private CV document services
builder.Services.AddScoped<ICvDocumentService, CvDocumentService>();
builder.Services.AddScoped<IApplicationService, ApplicationService>();
builder.Services.AddScoped<ICvReviewService, CvReviewService>();
builder.Services.AddScoped<ICvExtractionService, CvExtractionService>();
builder.Services.AddScoped<ICvMatchingService, CvMatchingService>();
builder.Services.AddScoped<ISkillCatalogueService, SkillCatalogueService>();
builder.Services.AddScoped<CvExtractionProcessor>();
builder.Services.AddHostedService<CvExtractionWorker>();
builder.Services.AddScoped<ICvFileStorage, CvFileStorage>();
builder.Services.AddScoped<ICvFileValidator, CvFileValidator>();
builder.Services.AddScoped<ICvMalwareScanner, ClamAvMalwareScanner>();

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

    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint(
            "/openapi/v1.json",
            "NexaHR API v1");

        options.RoutePrefix = "swagger";
        options.DocumentTitle = "NexaHR API Documentation";
    });
}

// Configure middleware
app.UseHttpsRedirection();

app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
