namespace NexaHR.Application.DTOs.Accounts;

public sealed class AccountOperationResponse
{
    public bool Success { get; init; }

    public string Code { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public AccountResponse? Account { get; init; }
}
