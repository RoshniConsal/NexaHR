namespace NexaHR.Application.DTOs.Invitations;

public sealed class InvitationResponse
{
    public Guid InvitationId { get; init; }

    public string Email { get; init; } = string.Empty;

    public string Role { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public Guid IssuedByUserId { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    public DateTime ExpiresAtUtc { get; init; }

    public DateTime? AcceptedAtUtc { get; init; }

    public Guid? AcceptedByUserId { get; init; }

    public DateTime? RevokedAtUtc { get; init; }

    public Guid? RevokedByUserId { get; init; }

    public string? RevocationReason { get; init; }

    public Guid? ReissuedFromInvitationId { get; init; }
}

public sealed class InvitationListResponse
{
    public IReadOnlyList<InvitationResponse> Items
        { get; init; } = [];

    public int Page { get; init; }

    public int PageSize { get; init; }

    public int TotalCount { get; init; }
}

public sealed class InvitationPreviewResponse
{
    public bool IsValid { get; init; }

    public string Message { get; init; } = string.Empty;

    public string? MaskedEmail { get; init; }

    public string? Role { get; init; }

    public DateTime? ExpiresAtUtc { get; init; }
}

public sealed class InvitationOperationResponse
{
    public bool Success { get; init; }

    public string Code { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public bool EmailQueued { get; init; }

    public InvitationResponse? Invitation { get; init; }
}

public sealed class InvitationAcceptanceResponse
{
    public bool Success { get; init; }

    public string Code { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public Guid? UserId { get; init; }

    public Guid? InvitationId { get; init; }

    public string? Role { get; init; }

    public DateTime? AcceptedAtUtc { get; init; }
}
