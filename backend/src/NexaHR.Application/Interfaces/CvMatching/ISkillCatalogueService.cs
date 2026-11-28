using NexaHR.Application.DTOs.CvMatching;

namespace NexaHR.Application.Interfaces.CvMatching;

public interface ISkillCatalogueService
{
    Task<CvMatchingOperationResponse<
        SkillCatalogueResponse>>
        GetCurrentAsync(
            Guid actorUserId,
            CancellationToken cancellationToken);

    Task<CvMatchingOperationResponse<
        SkillCatalogueResponse>>
        PublishAsync(
            Guid actorUserId,
            PublishSkillCatalogueRequest request,
            CancellationToken cancellationToken);
}
