using NexaHR.Application.DTOs.Company;

namespace NexaHR.Application.Interfaces.Company;

public interface ICompanySettingsService
{
    Task<CompanySettingsResponse?> GetSettingsAsync(
        Guid userId);

    Task<CompanySettingsOperationResponse>
        UpdateSettingsAsync(
            Guid actorId,
            UpdateCompanySettingsRequest request);

    Task<SupportContactResponse?> GetSupportContactAsync(
        Guid userId);
}
