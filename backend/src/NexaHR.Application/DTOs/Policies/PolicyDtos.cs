namespace NexaHR.Application.DTOs.Policies;

public sealed record PolicyOperationResult<T>(
    bool Success,
    string Code,
    string Message,
    T? Data = default);

public sealed record PolicyListItem(
    Guid Id,
    string Title,
    int? CurrentPublishedVersion,
    bool RequiresAcknowledgement,
    bool IsAcknowledged,
    DateTime? PublishedAtUtc,
    bool? HasDraft);

public sealed record PolicyListResponse(
    IReadOnlyList<PolicyListItem> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record PolicyDraftResponse(
    string Title,
    string Content,
    bool RequiresAcknowledgement,
    bool VisibleToEmployees,
    bool VisibleToHr,
    bool VisibleToCeo);

public sealed record PolicyVersionSummary(
    int Version,
    string Title,
    bool RequiresAcknowledgement,
    DateTime PublishedAtUtc,
    bool IsAcknowledged,
    DateTime? AcknowledgedAtUtc);

public sealed record PolicyVersionResponse(
    Guid PolicyId,
    int Version,
    string Title,
    string Content,
    bool RequiresAcknowledgement,
    DateTime PublishedAtUtc,
    bool IsAcknowledged,
    DateTime? AcknowledgedAtUtc);

public sealed record PolicyDetailsResponse(
    Guid Id,
    int? CurrentPublishedVersion,
    PolicyVersionResponse? CurrentVersion,
    IReadOnlyList<PolicyVersionSummary> Versions,
    PolicyDraftResponse? Draft,
    string? RowVersion);

public sealed record PolicyAcknowledgementResponse(
    Guid PolicyId,
    int Version,
    DateTime AcknowledgedAtUtc);
