using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using NexaHR.Application.DTOs.Company;
using NexaHR.Application.Interfaces.Company;
using NexaHR.Domain.Constants;
using NexaHR.Domain.Entities;
using NexaHR.Infrastructure.Identity;
using NexaHR.Infrastructure.Persistence;

namespace NexaHR.Infrastructure.Services.Company;

public sealed class CompanySettingsService
    : ICompanySettingsService
{
    private readonly NexaHRDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public CompanySettingsService(
        NexaHRDbContext db,
        UserManager<ApplicationUser> users)
    {
        _db = db;
        _users = users;
    }

    private async Task<string?> GetRoleAsync(Guid userId)
    {
        var user = await _users.FindByIdAsync(
            userId.ToString());

        if (user is null ||
            !user.IsActive ||
            !user.EmailConfirmed ||
            await _users.IsLockedOutAsync(user))
        {
            return null;
        }

        var roles = await _users.GetRolesAsync(user);

        return roles.Count == 1 &&
            AppRoles.All.Contains(roles[0])
            ? roles[0]
            : null;
    }

    private static CompanySettingsResponse Map(
        CompanySettings? settings,
        bool isHr)
    {
        return new CompanySettingsResponse
        {
            CompanyName = settings?.CompanyName,
            SupportEmail = settings?.SupportEmail,
            SupportPhone = settings?.SupportPhone,
            BusinessTimeZone = "Asia/Colombo",
            IsConfigured = settings is not null,
            UpdatedAtUtc = isHr
                ? settings?.UpdatedAtUtc
                : null,
            RowVersion = isHr && settings is not null
                ? Convert.ToBase64String(settings.RowVersion)
                : null
        };
    }

    private static CompanySettingsOperationResponse Fail(
        string code,
        string message)
    {
        return new CompanySettingsOperationResponse
        {
            Success = false,
            Code = code,
            Message = message
        };
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    // API-020
    public async Task<CompanySettingsResponse?>
        GetSettingsAsync(Guid userId)
    {
        var role = await GetRoleAsync(userId);

        if (role is null)
            return null;

        var settings = await _db.CompanySettings
            .AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == 1);

        return Map(
            settings,
            role == AppRoles.HRManager);
    }

    // API-021
    public async Task<CompanySettingsOperationResponse>
        UpdateSettingsAsync(
            Guid actorId,
            UpdateCompanySettingsRequest request)
    {
        var role = await GetRoleAsync(actorId);

        if (role != AppRoles.HRManager)
        {
            return Fail(
                "forbidden",
                "Only HR Manager can update company settings.");
        }

        var companyName = request.CompanyName.Trim();

        if (companyName.Length < 2 ||
            companyName.Length > 200)
        {
            return Fail(
                "invalid_company_name",
                "Company name must contain 2 to 200 characters.");
        }

        var settings = await _db.CompanySettings
            .SingleOrDefaultAsync(c => c.Id == 1);

        if (settings is null)
        {
            if (!string.IsNullOrEmpty(request.RowVersion))
            {
                return Fail(
                    "concurrency_conflict",
                    "Reload company settings and try again.");
            }

            settings = new CompanySettings
            {
                Id = 1
            };

            _db.CompanySettings.Add(settings);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.RowVersion))
            {
                return Fail(
                    "concurrency_conflict",
                    "Reload the latest settings before updating.");
            }

            byte[] version;

            try
            {
                version = Convert.FromBase64String(
                    request.RowVersion);
            }
            catch (FormatException)
            {
                return Fail(
                    "invalid_row_version",
                    "Invalid settings version.");
            }

            if (version.Length != 8)
            {
                return Fail(
                    "invalid_row_version",
                    "Invalid settings version.");
            }

            if (!version.SequenceEqual(settings.RowVersion))
            {
                return Fail(
                    "concurrency_conflict",
                    "Settings have changed. Reload and try again.");
            }

            _db.Entry(settings)
                .Property(c => c.RowVersion)
                .OriginalValue = version;
        }

        settings.CompanyName = companyName;

        settings.SupportEmail =
            NormalizeOptional(request.SupportEmail);

        settings.SupportPhone =
            NormalizeOptional(request.SupportPhone);

        settings.BusinessTimeZone = "Asia/Colombo";

        settings.UpdatedAtUtc = DateTime.UtcNow;

        settings.UpdatedByUserId = actorId;

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Fail(
                "concurrency_conflict",
                "Settings have changed. Reload and try again.");
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is SqlException sql &&
                  (sql.Number == 2601 || sql.Number == 2627))
        {
            return Fail(
                "concurrency_conflict",
                "Settings were configured by another user. Reload.");
        }

        return new CompanySettingsOperationResponse
        {
            Success = true,
            Code = "success",
            Message = "Company settings updated successfully.",
            Settings = Map(settings, true)
        };
    }

    // API-022
    public async Task<SupportContactResponse?>
        GetSupportContactAsync(Guid userId)
    {
        var role = await GetRoleAsync(userId);

        if (role is null)
            return null;

        var settings = await _db.CompanySettings
            .AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == 1);

        var configured =
            !string.IsNullOrWhiteSpace(settings?.SupportEmail) ||
            !string.IsNullOrWhiteSpace(settings?.SupportPhone);

        return new SupportContactResponse
        {
            IsConfigured = configured,
            SupportEmail = settings?.SupportEmail,
            SupportPhone = settings?.SupportPhone,
            Message = configured
                ? "Contact HR using the provided details."
                : "Not configured"
        };
    }
}
