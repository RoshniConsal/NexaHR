using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NexaHR.Application.DTOs.CvAiAnalysis;
using NexaHR.Application.Interfaces.Audit;
using NexaHR.Application.Interfaces.CvAiAnalysis;
using NexaHR.Application.Services.CvAiAnalysis;
using NexaHR.Domain.Constants;
using NexaHR.Infrastructure.Identity;
using NexaHR.Infrastructure.Persistence;
using NexaHR.Infrastructure.Services.CvMatching;

namespace NexaHR.Infrastructure.Services.CvAiAnalysis;

public sealed partial class CvAiAnalysisService : ICvAiAnalysisService
{
    private readonly NexaHRDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IConfiguration _configuration;
    private readonly CvMatchingService _matching;
    private readonly AiProcessingAuthorizationService _authorization;
    private readonly IAiAnalysisProvider _provider;
    private readonly IAuditWriter _audit;

    public CvAiAnalysisService(
        NexaHRDbContext db,
        UserManager<ApplicationUser> users,
        IConfiguration configuration,
        CvMatchingService matching,
        AiProcessingAuthorizationService authorization,
        IAiAnalysisProvider provider,
        IAuditWriter audit)
    {
        _db = db;
        _users = users;
        _configuration = configuration;
        _matching = matching;
        _authorization = authorization;
        _provider = provider;
        _audit = audit;
    }

    private static CvAiOperationResponse<T> Success<T>(
        T data)
        where T : class
    {
        return new(
            true,
            "success",
            "Request completed.",
            data);
    }

    private static CvAiOperationResponse<T> Failure<T>(
        string code,
        string message = "AI operation is unavailable.")
        where T : class
    {
        return new(
            false,
            code,
            message,
            null);
    }

    private async Task<string?> GetActorRoleAsync(
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        if (actorUserId == Guid.Empty)
            return null;

        var actor = await _users.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == actorUserId,
                cancellationToken);

        if (actor is null ||
            !actor.IsActive ||
            !actor.EmailConfirmed ||
            await _users.IsLockedOutAsync(actor))
        {
            return null;
        }

        var roles = await _users.GetRolesAsync(actor);

        if (roles.Count != 1)
            return null;

        return roles[0];
    }

    private async Task<bool> IsAuthorizedHrAsync(
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        return await GetActorRoleAsync(
            actorUserId,
            cancellationToken) == AppRoles.HRManager;
    }

    private sealed record ConfigurationSnapshot(
        bool Enabled,
        string? Provider,
        string? Model,
        string? ModelDigest,
        string Code,
        AiConsentNoticeResponse? ConsentNotice);

    private async Task<ConfigurationSnapshot>
        LoadConfigurationAsync(
            CancellationToken cancellationToken)
    {
        var enabled = bool.TryParse(
            _configuration["Recruitment:Ai:Enabled"],
            out var parsed) && parsed;

        if (!enabled)
        {
            return new(
                false, null, null, null,
                "ai_disabled", null);
        }

        var provider =
            _configuration["Recruitment:Ai:Provider"];

        if (!string.Equals(
                provider,
                "Ollama",
                StringComparison.Ordinal))
        {
            return new(
                true, null, null, null,
                "ai_provider_not_configured", null);
        }

        Guid? approvedPolicyId =
            Guid.TryParse(
                _configuration[
                    "Recruitment:Ai:ApprovedPolicyId"],
                out var parsedPolicyId)
            && parsedPolicyId != Guid.Empty
                ? parsedPolicyId
                : null;

        var policy = await _db.ProcessingPolicies
            .AsNoTracking()
            .OrderByDescending(x => x.Version)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var policyCode = AiConsentGuard.ValidatePolicy(
            policy,
            provider!,
            approvedPolicyId,
            DateTime.UtcNow);

        if (policyCode != "success" || policy is null)
        {
            return new(
                true, null, null, null,
                policyCode, null);
        }

        var notice = new AiConsentNoticeResponse(
            policy.ProviderName,
            policy.Id,
            policy.Version,
            AiConsentGuard.GetExpectedTextVersion(policy),
            policy.ProcessingPurpose,
            policy.DataCategories,
            policy.ProviderRetention,
            policy.ProviderTrainingAllowed,
            policy.ProviderAccessPolicy,
            policy.ProcessingLocation,
            true);

        var model =
            _configuration["Recruitment:Ai:Model"];

        var digest =
            _configuration["Recruitment:Ai:ModelDigest"];

        if (string.IsNullOrWhiteSpace(model) ||
            model.Length > 200 ||
            string.IsNullOrWhiteSpace(digest) ||
            digest.Length != 64 ||
            !digest.All(Uri.IsHexDigit))
        {
            return new(
                true, provider, null, null,
                "ai_invalid_model_configuration",
                notice);
        }

        return new(
            true,
            provider,
            model,
            digest.ToUpperInvariant(),
            "success",
            notice);
    }
}
