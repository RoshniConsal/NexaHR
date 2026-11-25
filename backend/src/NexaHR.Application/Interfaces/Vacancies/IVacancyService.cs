using NexaHR.Application.DTOs.Vacancies;

namespace NexaHR.Application.Interfaces.Vacancies;

public interface IVacancyService
{
    Task<VacancyOperationResponse> CreateAsync(
        Guid actorUserId,
        CreateVacancyRequest request,
        CancellationToken cancellationToken = default);
}
