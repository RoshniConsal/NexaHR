using NexaHR.Application.DTOs.Invitations;

namespace NexaHR.Application.Interfaces.Invitations;

public interface IInvitationService
{
    Task<InvitationListResponse?> GetInvitationsAsync(
        Guid actorId,
        InvitationQuery query);

    Task<InvitationOperationResponse> IssueAsync(
        Guid actorId,
        IssueInvitationRequest request);

    Task<InvitationOperationResponse> ReissueAsync(
        Guid actorId,
        Guid invitationId,
        ReissueInvitationRequest request);

    Task<InvitationOperationResponse> RevokeAsync(
        Guid actorId,
        Guid invitationId,
        RevokeInvitationRequest request);

    Task<InvitationPreviewResponse> PreviewAsync(
        InvitationTokenRequest request);

    Task<InvitationAcceptanceResponse> AcceptAsync(
        Guid? userId,
        AcceptInvitationRequest request);
}
