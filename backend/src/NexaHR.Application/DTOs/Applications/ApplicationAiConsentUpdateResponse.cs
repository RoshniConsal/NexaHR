namespace NexaHR.Application.DTOs.Applications;

public sealed class ApplicationAiConsentUpdateResponse
{
    public Guid ApplicationId { get; init; }

    public ApplicationAiConsentResponse Consent
        { get; init; } = new();

    public string Stage { get; init; }
        = string.Empty;

    public string RowVersion { get; init; }
        = string.Empty;

    public bool Changed { get; init; }
}

public sealed class ApplicationAiConsentOperationResponse
{
    public bool Success { get; init; }

    public string Code { get; init; }
        = string.Empty;

    public string Message { get; init; }
        = string.Empty;

    public ApplicationAiConsentUpdateResponse? Data
        { get; init; }
}
