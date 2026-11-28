namespace NexaHR.Application.DTOs.Applications;

public sealed class ApplicationWithdrawalResponse
{
    public Guid ApplicationId { get; init; }

    public Guid EventId { get; init; }

    public string PreviousStage { get; init; }
        = string.Empty;

    public string CurrentStage { get; init; }
        = string.Empty;

    public DateTime AtUtc { get; init; }

    public string RowVersion { get; init; }
        = string.Empty;
}

public sealed class ApplicationWithdrawalOperationResponse
{
    public bool Success { get; init; }

    public string Code { get; init; }
        = string.Empty;

    public string Message { get; init; }
        = string.Empty;

    public ApplicationWithdrawalResponse? Data
        { get; init; }
}
