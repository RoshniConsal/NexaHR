using NexaHR.Application.DTOs.Vacancies;

namespace NexaHR.Application.Interfaces.Vacancies;

public interface IVacancyService
{
    Task<VacancyListResponse?> GetVacanciesAsync(
        Guid actorUserId,
        VacancyQuery query,
        CancellationToken cancellationToken = default);

    Task<VacancyOperationResponse> CreateAsync(
        Guid actorUserId,
        CreateVacancyRequest request,
        CancellationToken cancellationToken = default);

    Task<VacancyDetailsResponse?> GetVacancyDetailsAsync(
        Guid actorUserId,
        Guid vacancyId,
        CancellationToken cancellationToken = default);

    Task<UpdateVacancyOperationResponse> UpdateAsync(
        Guid actorUserId,
        Guid vacancyId,
        UpdateVacancyRequest request,
        CancellationToken cancellationToken = default);

    Task<VacancyRevisionHistoryResponse?> GetRevisionHistoryAsync(
        Guid actorUserId,
        Guid vacancyId,
        CancellationToken cancellationToken = default);

    Task<SubmitVacancyOperationResponse> SubmitAsync(
        Guid actorUserId,
        Guid vacancyId,
        SubmitVacancyRequest request,
        CancellationToken cancellationToken = default);

    Task<DecideVacancyOperationResponse> DecideAsync(
        Guid actorUserId,
        Guid vacancyId,
        DecideVacancyRequest request,
        CancellationToken cancellationToken = default);
}
